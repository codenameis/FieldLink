using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>FX CPU 프로그래밍 포트의 직접 연결 프로토콜을 사용합니다. 포트 설정과 개폐는 호출자가 담당합니다.</summary>
    public sealed class MelsecFxSerialClient
    {
        private readonly ISerialClient transport;
        private readonly SerialProtocolExchange exchange;
        private readonly IFrameBoundary boundary = new FxBoundary();
        private readonly TimeSpan timeout;
        private readonly bool isNewVersion;
        /// <summary>FX3U 방식(기본) 또는 FX2N 방식의 명령을 선택합니다.</summary>
        public MelsecFxSerialClient(ISerialClient transport, bool isNewVersion = true, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.isNewVersion = isNewVersion; this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            exchange = new SerialProtocolExchange(transport, boundary, this.timeout);
        }
        /// <summary>연속 워드를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken token = default(CancellationToken)) =>
            exchange.ExecuteSequenceAsync(MelsecFxSerialCommandBuilder.BuildReadWordCommand(address, length, isNewVersion), (i, r) => ParseRead(r), token);
        /// <summary>연속 비트를 읽습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken token = default(CancellationToken))
        {
            var command = MelsecFxSerialCommandBuilder.BuildReadBoolCommand(address, length, isNewVersion);
            if (!command.IsSuccess)
                return command.ConvertFailed<bool[]>();
            var data = await exchange.ExecuteSequenceAsync(OperationResult.CreateSuccessResult(command.Content1), (i, r) => ParseRead(r), token).ConfigureAwait(false);
            return data.IsSuccess ? OperationResult.CreateSuccessResult(data.Content.ToBoolArray().SelectMiddle(command.Content2, length)) : data.ConvertFailed<bool[]>();
        }
        /// <summary>워드를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken token = default(CancellationToken)) => exchange.ExecuteAsync(MelsecFxSerialCommandBuilder.BuildWriteWordCommand(address, value, isNewVersion), MelsecFxSerialResponseParser.CheckPlcWriteResponse, token);
        /// <summary>비트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken token = default(CancellationToken)) => exchange.ExecuteAsync(MelsecFxSerialCommandBuilder.BuildWriteBoolPacket(address, value), MelsecFxSerialResponseParser.CheckPlcWriteResponse, token);
        /// <summary>ENQ 확인 후 활성화 명령을 두 번 교환하는 원본 절차를 실행합니다.</summary>
        public Task<OperationResult> ActivatePlcAsync(CancellationToken token = default(CancellationToken)) => transport.ExecuteTransactionAsync(async tx =>
        {
            var check = FxSerialActivationCommandBuilder.CheckActivationAck(await tx.ExchangeAsync(FxSerialSessionCommandBuilder.BuildEnquiry(), boundary).ConfigureAwait(false));
            if (!check.IsSuccess)
                return check;
            // 활성화 명령의 두 응답은 원본과 같이 수신 성공 여부만 사용합니다.
            await tx.ExchangeAsync(FxSerialActivationCommandBuilder.BuildActivation(), boundary).ConfigureAwait(false);
            await tx.ExchangeAsync(FxSerialActivationCommandBuilder.BuildActivation(), boundary).ConfigureAwait(false);
            return OperationResult.CreateSuccessResult();
        }, timeout, token);
        /// <summary>현재 열린 9600 baud 포트에서 장치 속도를 변경합니다. 성공 후 포트를 닫고 새 속도로 명시적으로 다시 여세요.</summary>
        public Task<OperationResult> ChangeDeviceBaudRateAsync(int baudRate, CancellationToken token = default(CancellationToken))
        {
            if (baudRate != 19200 && baudRate != 38400 && baudRate != 57600 && baudRate != 115200)
                throw new ArgumentOutOfRangeException(nameof(baudRate));
            return transport.ExecuteTransactionAsync(async tx =>
            {
                OperationResult ack = null;
                // 원본 속도 협상은 비-ACK에만 ENQ를 최대 3회 보냅니다. 통신 예외에는 재전송하지 않습니다.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ack = FxSerialActivationCommandBuilder.CheckActivationAck(await tx.ExchangeAsync(FxSerialSessionCommandBuilder.BuildEnquiry(), boundary).ConfigureAwait(false));
                    if (ack.IsSuccess)
                        break;
                }
                if (!ack.IsSuccess)
                    return ack;
                return FxSerialActivationCommandBuilder.CheckActivationAck(await tx.ExchangeAsync(FxSerialSessionCommandBuilder.BuildBaudRateChange(baudRate), boundary).ConfigureAwait(false));
            }, timeout, token);
        }
        private static OperationResult<byte[]> ParseRead(byte[] response)
        {
            var check = MelsecFxSerialResponseParser.CheckPlcReadResponse(response);
            return check.IsSuccess ? MelsecFxSerialResponseParser.ExtractActualData(response) : check.ConvertFailed<byte[]>();
        }
        private sealed class FxBoundary : IFrameBoundary
        {
            public int? GetFrameLength(ArraySegment<byte> data)
            {
                if (data.Count == 0)
                    return null;
                byte first = data.Array[data.Offset];
                if (first == 6 || first == 0x15)
                    return 1;
                if (first != 2)
                    throw new InvalidDataException("FX 응답 시작 바이트가 올바르지 않습니다.");
                for (int i = 1; i < data.Count; i++)
                    if (data.Array[data.Offset + i] == 3)
                        return i + 3;
                return null;
            }
        }
    }
}
