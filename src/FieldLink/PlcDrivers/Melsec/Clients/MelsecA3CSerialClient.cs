using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>A3C 포맷 1~4의 MC ASCII 읽기·쓰기와 원격 제어를 교환합니다.</summary>
    public sealed class MelsecA3CSerialClient
    {
        private readonly ISerialClient transport;
        private readonly A3CFrameOptions options;
        private readonly TimeSpan timeout;
        private readonly IFrameBoundary boundary;
        /// <summary>원본의 무수신 간격 판정을 기본으로 사용합니다. 장비에 맞는 경계를 직접 지정할 수도 있습니다.</summary>
        public MelsecA3CSerialClient(ISerialClient transport, A3CFrameOptions options = null, TimeSpan? timeout = null, IFrameBoundary boundary = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            options = options ?? new A3CFrameOptions();
            if (options.Format < 1 || options.Format > 4)
                throw new ArgumentOutOfRangeException(nameof(options));
            this.options = new A3CFrameOptions
            {
                Station = options.Station,
                Format = options.Format,
                SumCheck = options.SumCheck,
                EnableWriteBitToWordRegister = options.EnableWriteBitToWordRegister
            };
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            this.boundary = boundary ?? new IdleGapFrame(TimeSpan.FromMilliseconds(40));
        }

        /// <summary>프로토콜의 읽기 상한마다 분할하여 워드를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            var commands = PlanRead(address, length, false);
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<byte[]>());
            return transport.ExecuteTransactionAsync(tx => ReadInsideAsync(tx, commands.Content, false), timeout, cancellationToken);
        }

        /// <summary>비트 장치 또는 D100.3과 같은 워드 내 비트 구간을 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            int dot = address.LastIndexOf('.');
            int bit = dot < 0 ? 0 : AddressParameters.CalculateBitStartIndex(address.Substring(dot + 1));
            if (bit < 0 || bit > 15)
                return Task.FromResult(new OperationResult<bool[]>("워드 비트 인덱스는 0~15여야 합니다."));
            var commands = PlanRead(dot < 0 ? address : address.Substring(0, dot), dot < 0 ? length : (ushort)((bit + length + 15) / 16), dot < 0);
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<bool[]>());
            return transport.ExecuteTransactionAsync(async tx =>
            {
                if (dot < 0)
                {
                    var bits = await ReadInsideAsync(tx, commands.Content, true).ConfigureAwait(false);
                    return bits.IsSuccess ? OperationResult.CreateSuccessResult(bits.Content.Select(b => b == '1').ToArray()) : bits.ConvertFailed<bool[]>();
                }

                var words = await ReadInsideAsync(tx, commands.Content, false).ConfigureAwait(false);
                return words.IsSuccess ? OperationResult.CreateSuccessResult(words.Content.ToBoolArray().SelectMiddle(bit, length)) : words.ConvertFailed<bool[]>();
            }, timeout, cancellationToken);
        }

        /// <summary>워드 데이터를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            byte station = UnitFor(ref address);
            var parsed = McDeviceAddress.ParseMelsecFrom(address, 0, false);
            if (!parsed.IsSuccess)
                return Task.FromResult((OperationResult)parsed);
            byte[] command = McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(parsed.Content, value);
            return transport.ExecuteTransactionAsync(tx => WriteCoreAsync(tx, command, station), timeout, cancellationToken);
        }

        /// <summary>비트를 기록합니다. 옵션이 허용하면 점 주소는 같은 독점 구간에서 워드를 읽고 변경한 뒤 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            int dot = address.LastIndexOf('.');
            bool wordBits = options.EnableWriteBitToWordRegister && dot >= 0;
            if (!wordBits)
            {
                byte station = UnitFor(ref address);
                var parsed = McDeviceAddress.ParseMelsecFrom(address, 0, true);
                if (!parsed.IsSuccess)
                    return Task.FromResult((OperationResult)parsed);
                byte[] command = McAsciiCommandBuilder.BuildAsciiWriteBitCoreCommand(parsed.Content, value);
                return transport.ExecuteTransactionAsync(tx => WriteCoreAsync(tx, command, station), timeout, cancellationToken);
            }

            int bit = AddressParameters.CalculateBitStartIndex(address.Substring(dot + 1));
            if (bit < 0 || bit > 15)
                return Task.FromResult(new OperationResult("워드 비트 인덱스는 0~15여야 합니다."));
            string wordAddress = address.Substring(0, dot);
            ushort wordLength = checked((ushort)(((long)bit + value.Length + 15) / 16));
            var commands = PlanRead(wordAddress, wordLength, false);
            if (!commands.IsSuccess)
                return Task.FromResult((OperationResult)commands);
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var words = await ReadInsideAsync(tx, commands.Content, false).ConfigureAwait(false);
                if (!words.IsSuccess)
                    return (OperationResult)words;
                for (int i = 0; i < value.Length; i++)
                {
                    int index = (bit + i) / 8;
                    byte mask = (byte)(1 << ((bit + i) % 8));
                    words.Content[index] = value[i] ? (byte)(words.Content[index] | mask) : (byte)(words.Content[index] & ~mask);
                }

                return await WriteWordsInsideAsync(tx, wordAddress, words.Content).ConfigureAwait(false);
            }, timeout, cancellationToken);
        }

        /// <summary>PLC 운전을 시작합니다.</summary>
        public Task<OperationResult> RemoteRunAsync(CancellationToken cancellationToken = default(CancellationToken)) => transport.ExecuteTransactionAsync(tx => WriteCoreAsync(tx, McControlCodec.BuildRemoteRun(McType.MCAscii).Content, options.Station), timeout, cancellationToken);
        /// <summary>PLC 운전을 정지합니다.</summary>
        public Task<OperationResult> RemoteStopAsync(CancellationToken cancellationToken = default(CancellationToken)) => transport.ExecuteTransactionAsync(tx => WriteCoreAsync(tx, McControlCodec.BuildRemoteStop(McType.MCAscii).Content, options.Station), timeout, cancellationToken);
        /// <summary>PLC 모델명 16문자를 읽습니다.</summary>
        public Task<OperationResult<string>> ReadPlcTypeAsync(CancellationToken cancellationToken = default(CancellationToken)) => transport.ExecuteTransactionAsync(async tx =>
        {
            var response = await ReadCoreAsync(tx, McControlCodec.BuildReadPlcType(McType.MCAscii).Content, options.Station).ConfigureAwait(false);
            if (!response.IsSuccess)
                return response.ConvertFailed<string>();
            return response.Content.Length >= 16 ? OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(response.Content, 0, 16).TrimEnd()) : new OperationResult<string>("A3C 모델명 응답이 16문자보다 짧습니다.");
        }, timeout, cancellationToken);
        private OperationResult<List<byte[]>> PlanRead(string address, ushort length, bool bits)
        {
            byte station = UnitFor(ref address);
            var parsed = McDeviceAddress.ParseMelsecFrom(address, length, bits);
            if (!parsed.IsSuccess)
                return parsed.ConvertFailed<List<byte[]>>();
            var result = new List<byte[]>();
            int limit = bits ? McValueConverter.GetReadBoolLength(McType.MCAscii) : McValueConverter.GetReadWordLength(McType.MCAscii);
            for (int completed = 0; completed < length;)
            {
                ushort count = (ushort)Math.Min(length - completed, limit);
                parsed.Content.Length = count;
                result.Add(MelsecA3CNetCommandBuilder.PackCommand(options, McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(parsed.Content, bits), station));
                completed += count;
                parsed.Content.AddressStart += bits || parsed.Content.McDataType.DataType == 0 ? count : count * 16;
            }

            return OperationResult.CreateSuccessResult(result);
        }

        private async Task<OperationResult<byte[]>> ReadInsideAsync(ISerialTransaction tx, List<byte[]> commands, bool bits)
        {
            var result = new List<byte>();
            foreach (byte[] command in commands)
            {
                var response = MelsecA3CNetResponseParser.ExtraReadActualResponse(options, await tx.ExchangeAsync(command, boundary).ConfigureAwait(false));
                if (!response.IsSuccess)
                    return response;
                result.AddRange(bits ? response.Content : MelsecValueConverter.TransAsciiByteArrayToByteArray(response.Content));
            }

            return OperationResult.CreateSuccessResult(result.ToArray());
        }

        private async Task<OperationResult> WriteWordsInsideAsync(ISerialTransaction tx, string address, byte[] value)
        {
            byte station = UnitFor(ref address);
            var parsed = McDeviceAddress.ParseMelsecFrom(address, 0, false);
            if (!parsed.IsSuccess)
                return parsed;
            return await WriteCoreAsync(tx, McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(parsed.Content, value), station).ConfigureAwait(false);
        }

        private async Task<OperationResult<byte[]>> ReadCoreAsync(ISerialTransaction tx, byte[] core, byte station) => MelsecA3CNetResponseParser.ExtraReadActualResponse(options, await tx.ExchangeAsync(MelsecA3CNetCommandBuilder.PackCommand(options, core, station), boundary).ConfigureAwait(false));
        private async Task<OperationResult> WriteCoreAsync(ISerialTransaction tx, byte[] core, byte station) => MelsecA3CNetResponseParser.CheckWriteResponse(options, await tx.ExchangeAsync(MelsecA3CNetCommandBuilder.PackCommand(options, core, station), boundary).ConfigureAwait(false));
        private byte UnitFor(ref string address) => (byte)AddressParameters.ExtractParameter(ref address, "s", options.Station);
    }
}
