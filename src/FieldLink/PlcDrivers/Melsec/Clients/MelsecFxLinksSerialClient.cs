using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>FX Links 포맷 1·4의 워드·비트와 PLC 운전 제어를 교환합니다.</summary>
    public sealed class MelsecFxLinksSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        private readonly FxLinksFrameOptions options;
        /// <summary>설정을 복사하여 사용합니다. 생성 후 외부 옵션 변경은 진행 중인 프레임에 영향을 주지 않습니다.</summary>
        public MelsecFxLinksSerialClient(ISerialClient transport, FxLinksFrameOptions options = null, TimeSpan? timeout = null)
        {
            options = options ?? new FxLinksFrameOptions();
            if (options.Format != 1 && options.Format != 4)
                throw new ArgumentOutOfRangeException(nameof(options));
            this.options = new FxLinksFrameOptions { Station = options.Station, Format = options.Format, SumCheck = options.SumCheck, WaittingTime = (byte)Math.Min(15, (int)options.WaittingTime) };
            exchange = new SerialProtocolExchange(transport, new ResponseBoundary(this.options), timeout);
        }
        /// <summary>워드 데이터를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return exchange.ExecuteSequenceAsync(Pack(MelsecFxLinksCommandBuilder.BuildReadCommand(station, address, length, false, options.WaittingTime)),
                (i, response) =>
                {
                    var checkedResponse = MelsecFxLinksResponseParser.CheckPlcResponse(response);
                    return checkedResponse.IsSuccess ? MelsecFxLinksResponseParser.ExtraResponse(checkedResponse.Content) : checkedResponse;
                }, cancellationToken);
        }
        /// <summary>연속 비트를 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            int dot = address.LastIndexOf('.');
            if (dot >= 0)
                return ReadWordBitsAsync(address, dot, length, cancellationToken);
            byte station = StationFor(ref address);
            return exchange.ExecuteSequenceAsync(Pack(MelsecFxLinksCommandBuilder.BuildReadCommand(station, address, length, true, options.WaittingTime)),
                (i, response) =>
                {
                    var check = MelsecFxLinksResponseParser.CheckPlcResponse(response);
                    return check.IsSuccess ? OperationResult.CreateSuccessResult(check.Content.Select(b => b == '1').ToArray()) : check.ConvertFailed<bool[]>();
                }, cancellationToken);
        }
        private async Task<OperationResult<bool[]>> ReadWordBitsAsync(string address, int dot, ushort length, CancellationToken token)
        {
            int bit = AddressParameters.CalculateBitStartIndex(address.Substring(dot + 1));
            if (bit < 0 || bit > 15)
                return new OperationResult<bool[]>("워드 비트 인덱스는 0~15여야 합니다.");
            // ReadAsync가 분할 워드 읽기 전체를 하나의 트랜잭션으로 실행합니다. s= 국번도 그대로 전달합니다.
            var words = await ReadAsync(address.Substring(0, dot), (ushort)((bit + length + 15) / 16), token).ConfigureAwait(false);
            return words.IsSuccess ? OperationResult.CreateSuccessResult(words.Content.ToBoolArray().SelectMiddle(bit, length)) : words.ConvertFailed<bool[]>();
        }
        /// <summary>워드 데이터를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return WriteCommandAsync(MelsecFxLinksCommandBuilder.BuildWriteByteCommand(station, address, value, options.WaittingTime), cancellationToken);
        }
        /// <summary>비트 배열을 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return WriteCommandAsync(MelsecFxLinksCommandBuilder.BuildWriteBoolCommand(station, address, value, options.WaittingTime), cancellationToken);
        }
        /// <summary>PLC 운전을 시작합니다. parameter에서 s 국번을 재정의할 수 있습니다.</summary>
        public Task<OperationResult> StartPlcAsync(string parameter = "", CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteCommandAsync(MelsecFxLinksCommandBuilder.BuildStart(StationFor(ref parameter), options.WaittingTime), cancellationToken);
        /// <summary>PLC 운전을 정지합니다.</summary>
        public Task<OperationResult> StopPlcAsync(string parameter = "", CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteCommandAsync(MelsecFxLinksCommandBuilder.BuildStop(StationFor(ref parameter), options.WaittingTime), cancellationToken);
        /// <summary>PLC 모델명을 조회합니다.</summary>
        public Task<OperationResult<string>> ReadPlcTypeAsync(string parameter = "", CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(Pack(MelsecFxLinksCommandBuilder.BuildReadPlcType(StationFor(ref parameter), options.WaittingTime)), response =>
            {
                var check = MelsecFxLinksResponseParser.CheckPlcResponse(response);
                if (!check.IsSuccess)
                    return check.ConvertFailed<string>();
                return check.Content.Length >= 2 ? MelsecFxLinksResponseParser.GetPlcTypeFromCode(Encoding.ASCII.GetString(check.Content, 0, 2))
                    : new OperationResult<string>("FX Links 모델 응답이 짧습니다.");
            }, cancellationToken);
        private async Task<OperationResult> WriteCommandAsync(OperationResult<byte[]> command, CancellationToken token) =>
            await exchange.ExecuteAsync(Pack(command), MelsecFxLinksResponseParser.CheckPlcResponse, token).ConfigureAwait(false);
        private OperationResult<byte[]> Pack(OperationResult<byte[]> command) => command.IsSuccess
            ? OperationResult.CreateSuccessResult(MelsecFxLinksCommandBuilder.PackCommandWithHeader(options, command.Content)) : command;
        private OperationResult<List<byte[]>> Pack(OperationResult<List<byte[]>> commands) => commands.IsSuccess
            ? OperationResult.CreateSuccessResult(commands.Content.Select(c => MelsecFxLinksCommandBuilder.PackCommandWithHeader(options, c)).ToList()) : commands;
        private byte StationFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", options.Station);
        }
        private sealed class ResponseBoundary : IFrameBoundary
        {
            private readonly FxLinksFrameOptions options;
            private readonly DelimitedFrame data;
            internal ResponseBoundary(FxLinksFrameOptions options)
            {
                this.options = options;
                data = options.Format == 4 ? new DelimitedFrame(new byte[] { 13, 10 }) : new DelimitedFrame(new byte[] { 3 }, options.SumCheck ? 2 : 0);
            }
            public int? GetFrameLength(ArraySegment<byte> bytes)
            {
                if (bytes.Count == 0)
                    return null;
                if (options.Format == 4)
                    return data.GetFrameLength(bytes);
                switch (bytes.Array[bytes.Offset])
                {
                    case 6: return 5;
                    case 21: return 7;
                    case 2: return data.GetFrameLength(bytes);
                    default: throw new InvalidDataException("FX Links 응답의 시작 제어 문자가 잘못되었습니다.");
                }
            }
        }
    }
}
