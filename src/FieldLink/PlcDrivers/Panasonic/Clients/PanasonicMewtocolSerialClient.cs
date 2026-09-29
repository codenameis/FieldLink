using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Panasonic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Panasonic.Clients
{
    /// <summary>Panasonic MEWTOCOL의 워드·비트·이산 주소와 모델 조회를 교환합니다.</summary>
    public sealed class PanasonicMewtocolSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 기본 국번을 지정합니다.</summary>
        public PanasonicMewtocolSerialClient(ISerialClient transport, byte station = 238, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>워드 데이터를 순서대로 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return exchange.ExecuteSequenceAsync(PanasonicCommandBuilder.BuildReadCommand(station, address, length, false),
                (i, response) => PanasonicResponseParser.ExtraActualData(response), cancellationToken);
        }
        /// <summary>비트 하나를 전용 명령으로 읽습니다.</summary>
        public async Task<OperationResult<bool>> ReadBoolAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            var read = await exchange.ExecuteAsync(PanasonicCommandBuilder.BuildReadOneCoil(station, address),
                PanasonicResponseParser.ExtraActualBool, cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<bool>();
            return read.Content.Length > 0 ? OperationResult.CreateSuccessResult(read.Content[0]) : new OperationResult<bool>("비트 응답이 비어 있습니다.");
        }
        /// <summary>연속 영역에서 요청한 비트 구간을 읽습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            var analysis = PanasonicAddressParser.AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return analysis.ConvertFailed<bool[]>();
            var read = await exchange.ExecuteSequenceAsync(PanasonicCommandBuilder.BuildReadCommand(station, address, length, true),
                (i, response) => PanasonicResponseParser.ExtraActualData(response), cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.ToBoolArray().SelectMiddle(analysis.Content2 % 16, length)) : read.ConvertFailed<bool[]>();
        }
        /// <summary>여러 이산 주소의 비트를 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string[] address, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteSequenceAsync(PanasonicCommandBuilder.BuildReadCoils(Station, address),
                (i, response) => PanasonicResponseParser.ExtraActualBool(response), cancellationToken);
        /// <summary>워드 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return await exchange.ExecuteAsync(PanasonicCommandBuilder.BuildWriteCommand(station, address, value),
                response => PanasonicResponseParser.ExtraActualData(response), cancellationToken).ConfigureAwait(false);
        }
        /// <summary>비트 하나를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return await exchange.ExecuteAsync(PanasonicCommandBuilder.BuildWriteOneCoil(station, address, value),
                response => PanasonicResponseParser.ExtraActualData(response), cancellationToken).ConfigureAwait(false);
        }
        /// <summary>16비트 경계에 맞는 연속 비트를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool[] values, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            byte station = StationFor(ref address);
            var analysis = PanasonicAddressParser.AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return analysis;
            if (analysis.Content2 % 16 != 0 || values.Length % 16 != 0)
                return new OperationResult("연속 비트 쓰기의 시작 주소와 길이는 16의 배수여야 합니다.");
            return await exchange.ExecuteAsync(PanasonicCommandBuilder.BuildWriteCommand(station, address, ProtocolBytes.BoolArrayToByte(values)),
                response => PanasonicResponseParser.ExtraActualData(response), cancellationToken).ConfigureAwait(false);
        }
        /// <summary>여러 이산 주소의 비트를 순서대로 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string[] address, bool[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            await exchange.ExecuteSequenceAsync(PanasonicCommandBuilder.BuildWriteCoils(Station, address, values),
                (i, response) => PanasonicResponseParser.ExtraActualData(response), cancellationToken).ConfigureAwait(false);
        /// <summary>PLC 모델명을 조회합니다.</summary>
        public Task<OperationResult<string>> ReadPlcTypeAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(PanasonicCommandBuilder.BuildReadPlcModel(Station), response =>
            {
                var raw = PanasonicResponseParser.ExtraActualData(response, false);
                return raw.IsSuccess ? MewtocolResponseParser.GetPlcType(raw.Content) : raw.ConvertFailed<string>();
            }, cancellationToken);
        private byte StationFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
        }
    }
}
