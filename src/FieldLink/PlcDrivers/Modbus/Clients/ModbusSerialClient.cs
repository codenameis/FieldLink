using FieldLink.PlcDrivers.Modbus;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Modbus.Clients
{
    /// <summary>Modbus RTU·ASCII 읽기와 쓰기를 조율합니다. 주입한 포트의 열기·종료는 호출자가 담당합니다.</summary>
    public class ModbusSerialClient
    {
        private readonly ISerialClient transport;
        private readonly IFrameBoundary boundary;
        private readonly Func<string, byte, OperationResult<string>> addressMapping;
        private readonly TimeSpan timeout;
        private readonly ModbusSerialEncoding encoding;

        /// <summary>외피와 국번, 선택적인 제조사 주소 변환을 지정합니다. 전체 작업에 하나의 제한 시간을 적용합니다.</summary>
        public ModbusSerialClient(ISerialClient transport, ModbusSerialEncoding encoding = ModbusSerialEncoding.Rtu,
            byte station = 1, bool addressStartWithZero = true, TimeSpan? timeout = null,
            Func<string, byte, OperationResult<string>> addressMapping = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (!Enum.IsDefined(typeof(ModbusSerialEncoding), encoding))
                throw new ArgumentOutOfRangeException(nameof(encoding));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            this.encoding = encoding; Station = station; AddressStartWithZero = addressStartWithZero;
            this.addressMapping = addressMapping ?? ((address, function) => OperationResult.CreateSuccessResult(address));
            boundary = encoding == ModbusSerialEncoding.Ascii ? (IFrameBoundary)new DelimitedFrame(new byte[] { 13, 10 }) : new RtuResponseBoundary();
        }

        /// <summary>주소에 s 매개변수가 없을 때 사용하는 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>입력 주소의 0 기반 여부입니다.</summary>
        public bool AddressStartWithZero { get; }

        /// <summary>레지스터를 읽습니다. 주소의 x 매개변수로 기능 4 등을 선택할 수 있습니다.</summary>
        public virtual Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = Map(address, 3);
            if (!mapped.IsSuccess)
                return Task.FromResult(mapped.ConvertFailed<byte[]>());
            var commands = ModbusCommandBuilder.BuildReadModbusCommand(mapped.Content, length, Station, AddressStartWithZero, 3);
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<byte[]>());
            return transport.ExecuteTransactionAsync(tx => ReadWordsAsync(tx, commands.Content), timeout, cancellationToken);
        }

        /// <summary>코일 또는 레지스터의 점 주소 비트를 읽습니다.</summary>
        public virtual async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = Map(address, 1);
            if (!mapped.IsSuccess)
                return mapped.ConvertFailed<bool[]>();
            int dot = mapped.Content.LastIndexOf('.');
            if (dot > 0)
            {
                if (!int.TryParse(mapped.Content.Substring(dot + 1), out int bit) || bit < 0 || bit > 15)
                    return new OperationResult<bool[]>("레지스터의 비트 인덱스는 0~15여야 합니다.");
                int originalDot = address.LastIndexOf('.');
                var read = await ReadAsync(originalDot < 0 ? address : address.Substring(0, originalDot), (ushort)((length + bit + 15) / 16), cancellationToken).ConfigureAwait(false);
                if (!read.IsSuccess)
                    return read.ConvertFailed<bool[]>();
                return OperationResult.CreateSuccessResult(ProtocolBytes.BytesReverseByWord(read.Content).ToBoolArray().SelectMiddle(bit, length));
            }
            var commands = ModbusCommandBuilder.BuildReadModbusCommand(mapped.Content, length, Station, AddressStartWithZero, 1);
            if (!commands.IsSuccess)
                return commands.ConvertFailed<bool[]>();
            return await transport.ExecuteTransactionAsync(async tx =>
            {
                var bits = new List<bool>();
                foreach (var command in commands.Content)
                {
                    var response = await ExchangeAsync(tx, command).ConfigureAwait(false);
                    if (!response.IsSuccess)
                        return response.ConvertFailed<bool[]>();
                    bits.AddRange(ProtocolBytes.ByteToBoolArray(response.Content, command[4] * 256 + command[5]));
                }
                return OperationResult.CreateSuccessResult(bits.ToArray());
            }, timeout, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>여러 레지스터를 기록합니다. 입력은 Modbus의 워드 내 큰 자리 바이트 우선 순서입니다.</summary>
        public virtual Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0 || value.Length % 2 != 0 || value.Length > 246)
                throw new ArgumentOutOfRangeException(nameof(value));
            return WriteMappedAsync(address, 16, a => ModbusCommandBuilder.BuildWriteWordModbusCommand(a, value, Station, AddressStartWithZero, 16), cancellationToken);
        }

        /// <summary>기능 6으로 레지스터 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteMappedAsync(address, 6, a => ModbusCommandBuilder.BuildWriteWordModbusCommand(a, value, Station, AddressStartWithZero, 6, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap)), cancellationToken);

        /// <summary>코일 하나 또는 점 주소의 레지스터 비트 하나를 기록합니다. 마스크 쓰기 실패를 자동 재전송하지 않습니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteMappedAsync(address, 5, a => ModbusCommandBuilder.BuildWriteBoolModbusCommand(a, value, Station, AddressStartWithZero, 5), cancellationToken);

        /// <summary>코일 배열 또는 점 주소의 연속 비트를 기록합니다. 레지스터 갱신은 읽기·변경·쓰기를 한 독점 구간에서 수행합니다.</summary>
        public virtual async Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0 || value.Length > 1968)
                throw new ArgumentOutOfRangeException(nameof(value));
            cancellationToken.ThrowIfCancellationRequested();
            var mapped = Map(address, 15);
            if (!mapped.IsSuccess)
                return mapped;
            int dot = mapped.Content.LastIndexOf('.');
            if (dot < 0)
                return await WriteBuiltAsync(ModbusCommandBuilder.BuildWriteBoolModbusCommand(mapped.Content, value, Station, AddressStartWithZero, 15), cancellationToken).ConfigureAwait(false);
            if (!int.TryParse(mapped.Content.Substring(dot + 1), out int bit) || bit < 0 || bit > 15)
                return new OperationResult("레지스터의 비트 인덱스는 0~15여야 합니다.");
            if ((value.Length + bit + 15) / 16 > 123)
                throw new ArgumentOutOfRangeException(nameof(value), "레지스터 비트 갱신은 시작 비트를 포함하여 최대 123워드입니다.");
            int originalDot = address.LastIndexOf('.');
            var words = Map(originalDot < 0 ? address : address.Substring(0, originalDot), 3);
            if (!words.IsSuccess)
                return words;
            var commands = ModbusCommandBuilder.BuildReadModbusCommand(words.Content, (ushort)((value.Length + bit + 15) / 16), Station, AddressStartWithZero, 3);
            if (!commands.IsSuccess)
                return commands;
            return await transport.ExecuteTransactionAsync(async tx =>
            {
                var read = await ReadWordsAsync(tx, commands.Content).ConfigureAwait(false);
                if (!read.IsSuccess)
                    return (OperationResult)read;
                for (int i = 0; i < value.Length; i++)
                {
                    int position = bit + i, index = position / 16 * 2 + (position % 16 < 8 ? 1 : 0);
                    byte mask = (byte)(1 << (position % 8));
                    read.Content[index] = value[i] ? (byte)(read.Content[index] | mask) : (byte)(read.Content[index] & ~mask);
                }
                var write = ModbusCommandBuilder.BuildWriteWordModbusCommand(words.Content, read.Content, Station, AddressStartWithZero, 16);
                return write.IsSuccess ? await ExchangeAsync(tx, write.Content).ConfigureAwait(false) : (OperationResult)write;
            }, timeout, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>기능 22로 레지스터의 AND·OR 마스크를 기록합니다.</summary>
        public Task<OperationResult> WriteMaskAsync(string address, ushort andMask, ushort orMask, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteMappedAsync(address, 22, a => ModbusCommandBuilder.BuildWriteMaskModbusCommand(a, andMask, orMask, Station, AddressStartWithZero, 22), cancellationToken);

        /// <summary>기능 23으로 같은 요청 안에서 읽기와 쓰기를 수행합니다.</summary>
        public Task<OperationResult<byte[]>> ReadWriteAsync(string readAddress, ushort length, string writeAddress, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (length == 0 || length > 125 || value.Length == 0 || value.Length % 2 != 0 || value.Length > 242)
                throw new ArgumentOutOfRangeException(nameof(value));
            cancellationToken.ThrowIfCancellationRequested();
            var read = Map(readAddress, 23); var write = Map(writeAddress, 23);
            if (!read.IsSuccess)
                return Task.FromResult(read.ConvertFailed<byte[]>());
            if (!write.IsSuccess)
                return Task.FromResult(write.ConvertFailed<byte[]>());
            var command = ModbusCommandBuilder.BuildReadWriteModbusCommand(read.Content, length, write.Content, value, Station, AddressStartWithZero, 23);
            return command.IsSuccess ? ExecuteCoreAsync(command.Content, cancellationToken) : Task.FromResult(command);
        }

        /// <summary>완성된 Modbus 코어를 교환합니다. 반환값은 읽기 데이터 또는 빈 쓰기 승인입니다.</summary>
        public Task<OperationResult<byte[]>> ExecuteCoreAsync(byte[] core, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (core == null)
                throw new ArgumentNullException(nameof(core));
            if (core.Length < 2)
                throw new ArgumentException("국번과 기능 코드가 필요합니다.", nameof(core));
            return transport.ExecuteTransactionAsync(tx => ExchangeAsync(tx, core), timeout, cancellationToken);
        }

        private OperationResult<string> Map(string address, byte function)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return addressMapping(address, function);
        }
        private Task<OperationResult> WriteMappedAsync(string address, byte function, Func<string, OperationResult<byte[]>> build, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var mapped = Map(address, function);
            return mapped.IsSuccess ? WriteBuiltAsync(build(mapped.Content), token) : Task.FromResult((OperationResult)mapped);
        }
        private async Task<OperationResult> WriteBuiltAsync(OperationResult<byte[]> command, CancellationToken token) =>
            command.IsSuccess ? await ExecuteCoreAsync(command.Content, token).ConfigureAwait(false) : (OperationResult)command;
        private async Task<OperationResult<byte[]>> ReadWordsAsync(ISerialTransaction tx, byte[][] commands)
        {
            var bytes = new List<byte>();
            foreach (var command in commands)
            {
                var response = await ExchangeAsync(tx, command).ConfigureAwait(false);
                if (!response.IsSuccess)
                    return response;
                bytes.AddRange(response.Content);
            }
            return OperationResult.CreateSuccessResult(bytes.ToArray());
        }
        private async Task<OperationResult<byte[]>> ExchangeAsync(ISerialTransaction tx, byte[] core)
        {
            var raw = await tx.ExchangeAsync(ModbusSerialCodec.Encode(core, encoding), boundary).ConfigureAwait(false);
            var decoded = ModbusSerialCodec.Decode(raw, encoding);
            return decoded.IsSuccess ? ModbusResponseParser.Parse(core, decoded.Content, ModbusSerialCodec.ResponseLayout(core[1])) : decoded;
        }

        private sealed class RtuResponseBoundary : ISerialFrameTiming, ISerialReceiveTiming
        {
            public TimeSpan GetMaximumInterCharacterInterval(int baudRate, double bitsPerCharacter) =>
                ModbusSerialCodec.GetMaximumRtuInterCharacterInterval(baudRate, bitsPerCharacter);

            // Modbus Serial Line V1.02 §2.5.1.1: 저속 t3.5, 19200 초과는 1.750ms 권고값.
            public TimeSpan GetMinimumSilentInterval(int baudRate, double bitsPerCharacter) =>
                TimeSpan.FromTicks((long)Math.Ceiling((baudRate > 19200 ? 0.00175 : 3.5 * bitsPerCharacter / baudRate) * TimeSpan.TicksPerSecond));

            public int? GetFrameLength(ArraySegment<byte> bytes)
            {
                if (bytes.Count < 2)
                    return null;
                byte function = bytes.Array[bytes.Offset + 1];
                if (function >= 128)
                    return 5;
                function = ModbusSerialCodec.ResponseLayout(function);
                if (function == 1 || function == 2 || function == 3 || function == 4 || function == 23)
                    return bytes.Count < 3 ? (int?)null : 5 + bytes.Array[bytes.Offset + 2];
                if (function == 22)
                    return 10;
                if (function == 5 || function == 6 || function == 15 || function == 16)
                    return 8;
                throw new InvalidDataException("지원하지 않는 Modbus RTU 응답 기능입니다.");
            }
        }
    }
}
