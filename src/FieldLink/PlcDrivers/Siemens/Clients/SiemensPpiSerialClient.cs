using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Siemens;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Siemens.Clients
{
    /// <summary>PPI 요청, E5 확인, 실행 확인 요청을 하나의 독점 교환으로 처리합니다.</summary>
    public sealed class SiemensPpiSerialClient
    {
        private readonly ISerialClient transport;
        private readonly TimeSpan timeout;
        private readonly IFrameBoundary boundary = new PpiBoundary();
        /// <summary>기본 국번입니다. 주소의 s= 접두사로 요청별 지정이 가능합니다.</summary>
        public byte Station { get; }
        /// <summary>호출자가 관리하는 시리얼 전송을 지정합니다.</summary>
        public SiemensPpiSerialClient(ISerialClient transport, byte station = 2, TimeSpan? timeout = null)
        { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); Station = station; this.timeout = timeout ?? TimeSpan.FromSeconds(3); }
        /// <summary>바이트 단위로 연속 데이터를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return ExchangeAsync(SiemensPPICommandBuilder.BuildReadCommand(station, address, length, false), station, ParseBytes, token);
        }
        /// <summary>단일 비트 읽기 명령을 사용합니다.</summary>
        public Task<OperationResult<bool>> ReadBoolAsync(string address, CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return ExchangeAsync(SiemensPPICommandBuilder.BuildReadCommand(station, address, 1, true), station, response =>
            {
                var check = SiemensPPIResponseParser.CheckResponse(response);
                if (!check.IsSuccess)
                    return check.ConvertFailed<bool>();
                var data = SiemensS7ResponseParser.AnalysisReadBit(response);
                return data.IsSuccess ? OperationResult.CreateSuccessResult(data.Content.ToBoolArray()[0]) : data.ConvertFailed<bool>();
            }, token);
        }
        /// <summary>바이트 읽기를 사용해 연속 비트를 추출합니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            var parsed = SiemensPPIAddressParser.AnalysisAddress(address);
            if (!parsed.IsSuccess)
                return parsed.ConvertFailed<bool[]>();
            int offset = parsed.Content.AddressStart % 8;
            parsed.Content.AddressStart -= offset;
            ushort byteLength = checked((ushort)((offset + length + 7) / 8));
            var read = await ExchangeAsync(SiemensPPICommandBuilder.BuildReadCommand(station, parsed.Content, byteLength, false), station, ParseBytes, token).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.ToBoolArray().SelectMiddle(offset, length)) : read.ConvertFailed<bool[]>();
        }
        /// <summary>연속 바이트를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return WriteCoreAsync(SiemensPPICommandBuilder.BuildWriteCommand(station, address, value), station, token);
        }
        /// <summary>연속 비트를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return WriteCoreAsync(SiemensPPICommandBuilder.BuildWriteCommand(station, address, value), station, token);
        }
        /// <summary>비트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken token = default(CancellationToken)) => WriteAsync(address, new[] { value }, token);
        private async Task<OperationResult> WriteCoreAsync(OperationResult<byte[]> command, byte station, CancellationToken token) => await ExchangeAsync(command, station, r =>
        {
            var check = SiemensPPIResponseParser.CheckResponse(r);
            return check.IsSuccess ? OperationResult.CreateSuccessResult(true) : check.ConvertFailed<bool>();
        }, token).ConfigureAwait(false);
        /// <summary>RUN 명령을 보냅니다. 원본과 같이 두 번째 응답은 수신 여부만 확인합니다.</summary>
        public Task<OperationResult> StartAsync(string parameter = "", CancellationToken token = default(CancellationToken)) => ControlAsync(parameter, true, token);
        /// <summary>STOP 명령을 보냅니다. 원본과 같이 두 번째 응답은 수신 여부만 확인합니다.</summary>
        public Task<OperationResult> StopAsync(string parameter = "", CancellationToken token = default(CancellationToken)) => ControlAsync(parameter, false, token);
        private async Task<OperationResult> ControlAsync(string parameter, bool start, CancellationToken token)
        {
            byte confirmationStation = (byte)AddressParameters.ExtractParameter(ref parameter, "s", Station);
            // 원본은 제어 본문에 기본 국번, 확인 요청에만 s= 국번을 사용합니다.
            byte[] command = start ? PpiControlCodec.BuildStart(Station) : PpiControlCodec.BuildStop(Station);
            return await ExchangeAsync(OperationResult.CreateSuccessResult(command), confirmationStation, r => OperationResult.CreateSuccessResult(true), token).ConfigureAwait(false);
        }
        /// <summary>SYS0에서 PLC 기종 문자열을 읽습니다.</summary>
        public Task<OperationResult<string>> ReadPlcTypeAsync(string parameter = "", CancellationToken token = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref parameter, "s", Station);
            return ExchangeAsync(SiemensPPICommandBuilder.BuildReadCommand(station, "SYS0", 20, false), station, PpiModelResponseParser.Parse, token);
        }
        /// <summary>바이트 하나를 읽습니다.</summary>
        public async Task<OperationResult<byte>> ReadByteAsync(string address, CancellationToken token = default(CancellationToken))
        {
            var read = await ReadAsync(address, 1, token).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content[0]) : read.ConvertFailed<byte>();
        }
        /// <summary>바이트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte value, CancellationToken token = default(CancellationToken)) => WriteAsync(address, new[] { value }, token);
        private static OperationResult<byte[]> ParseBytes(byte[] response)
        {
            var check = SiemensPPIResponseParser.CheckResponse(response);
            return check.IsSuccess ? SiemensS7ResponseParser.AnalysisReadByte(response) : check.ConvertFailed<byte[]>();
        }
        private Task<OperationResult<T>> ExchangeAsync<T>(OperationResult<byte[]> command, byte station, Func<byte[], OperationResult<T>> parse, CancellationToken token)
        {
            if (!command.IsSuccess)
                return Task.FromResult(command.ConvertFailed<T>());
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var ack = PpiControlCodec.CheckAcknowledgement(await tx.ExchangeAsync(command.Content, boundary).ConfigureAwait(false));
                if (!ack.IsSuccess)
                    return ack.ConvertFailed<T>();
                return parse(await tx.ExchangeAsync(SiemensPPICommandBuilder.GetExecuteConfirm(station), boundary).ConfigureAwait(false));
            }, timeout, token);
        }
        private sealed class PpiBoundary : IFrameBoundary
        {
            public int? GetFrameLength(ArraySegment<byte> data)
            {
                if (data.Count == 0)
                    return null;
                if (data.Array[data.Offset] == 0xE5)
                    return 1;
                if (data.Array[data.Offset] != 0x68)
                    throw new InvalidDataException("PPI 응답의 시작 바이트가 올바르지 않습니다.");
                if (data.Count < 4)
                    return null;
                return data.Array[data.Offset + 1] + 6;
            }
        }
    }
}
