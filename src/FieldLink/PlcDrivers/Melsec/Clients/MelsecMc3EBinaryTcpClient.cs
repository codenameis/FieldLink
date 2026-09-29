using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>QnA 호환 MC 3E 바이너리 TCP로 한 블록의 워드를 읽습니다.</summary>
    /// <remarks>
    /// 전송 객체는 호출자가 소유하며 OpenAsync, Close, Dispose도 호출자가 수행합니다.
    /// 자동 연결·재접속·재전송과 분할 읽기는 수행하지 않습니다. 동시 요청은 주입한 전송의 순차 교환 계약을 따릅니다.
    /// </remarks>
    public sealed class MelsecMc3EBinaryTcpClient
    {
        /// <summary>참고 구현의 바이너리 읽기 블록 정책을 유지한 상한입니다. 장비별 지원 범위를 보장하지 않습니다.</summary>
        public const ushort MaximumReadWords = 950;
        private const int MaximumDeviceAddress = 0xFFFFFF;
        private readonly ITcpClient transport;
        private readonly McFrameOptions frameOptions;
        private readonly TimeSpan timeout;
        private readonly MelsecMc3EBinaryFrame responseBoundary = new MelsecMc3EBinaryFrame();
        private readonly ProtocolValueConverter byteTransform = new ProtocolValueConverter();

        /// <summary>호출자가 소유하는 TCP 전송과 복사해서 보관할 MC 경로 설정을 지정합니다.</summary>
        /// <param name="transport">명시적으로 연결하여 사용할 TCP 전송입니다.</param>
        /// <param name="frameOptions">MC 접근 경로입니다. 생략하면 기본 경로를 사용합니다.</param>
        /// <param name="timeout">교환의 대기·송신·수신에 적용할 전체 제한 시간입니다. 기본값은 3초입니다.</param>
        /// <exception cref="ArgumentNullException">전송 객체가 없습니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">제한 시간이 1~Int32.MaxValue 밀리초 범위를 벗어납니다.</exception>
        public MelsecMc3EBinaryTcpClient(ITcpClient transport, McFrameOptions frameOptions = null, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout < TimeSpan.FromMilliseconds(1) || this.timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            McFrameOptions source = frameOptions ?? new McFrameOptions();
            this.frameOptions = new McFrameOptions
            {
                NetworkNumber = source.NetworkNumber,
                PLCNumber = source.PLCNumber,
                TargetIOStation = source.TargetIOStation,
                NetworkStationNumber = source.NetworkStationNumber
            };
        }

        /// <summary>지정한 장치 주소부터 16비트 워드를 연속해서 읽습니다.</summary>
        /// <param name="address">D100, W100 등 기존 MC 주소 파서가 지원하는 일반 장치 주소입니다.</param>
        /// <param name="count">읽을 워드 수입니다. 1~950을 허용합니다.</param>
        /// <param name="cancellationToken">호출자가 요청한 취소입니다.</param>
        /// <returns>읽은 워드 배열 또는 주소 해석·PLC 명령의 실패 결과입니다. PLC 종료 코드를 보존합니다.</returns>
        /// <remarks>
        /// 비트 장치의 워드 읽기는 워드당 16비트를 읽습니다. 확장·모듈·태그 주소는 이번 API 범위에 포함하지 않습니다.
        /// 정상 장치 거절은 연결을 유지합니다. 형식·경로·데이터 길이가 잘못된 응답은 통신 계층에서 거부합니다.
        /// 응답 배열과 변환 결과는 호출별로 독립적입니다. 전송 예외를 실패 결과로 숨기지 않습니다.
        /// </remarks>
        /// <exception cref="ArgumentException">주소가 null 또는 공백입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">워드 수가 지원 범위를 벗어납니다.</exception>
        /// <exception cref="InvalidOperationException">전송이 Open 상태가 아닙니다.</exception>
        /// <exception cref="ObjectDisposedException">전송 객체가 해제되었습니다.</exception>
        /// <exception cref="OperationCanceledException">전송 계층에 진입하기 전에 이미 취소되었습니다.</exception>
        /// <exception cref="CommunicationException">교환 중 취소·시간 초과·연결 종료·잘못된 응답 등이 발생했습니다.</exception>
        public async Task<OperationResult<ushort[]>> ReadWordsAsync(string address, ushort count,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("장치 주소가 비어 있습니다.", nameof(address));
            if (count == 0 || count > MaximumReadWords)
                throw new ArgumentOutOfRangeException(nameof(count));
            ClientState state = transport.State;
            if (state == ClientState.Disposed)
                throw new ObjectDisposedException(nameof(transport));
            if (state != ClientState.Open)
                throw new InvalidOperationException("읽기 전에 TCP 전송의 OpenAsync를 완료하세요.");
            cancellationToken.ThrowIfCancellationRequested();

            OperationResult<McDeviceAddress> parsed = McDeviceAddress.ParseMelsecFrom(address, count, false);
            if (!parsed.IsSuccess)
                return OperationResult.CreateFailedResult<ushort[]>(parsed);
            int addressedPoints = parsed.Content.McDataType.DataType == 0 ? count : count * 16;
            if (parsed.Content.AddressStart < 0 || parsed.Content.AddressStart > MaximumDeviceAddress - addressedPoints + 1)
                return new OperationResult<ushort[]>("읽기 범위가 3E 프레임의 24비트 장치 주소 범위를 벗어납니다.");

            byte[] command = McBinaryCommandBuilder.BuildReadMcCoreCommand(parsed.Content, false);
            byte[] request = McBinaryCommandBuilder.PackMcCommand(frameOptions, command);
            // 진입 상태는 빠른 거부용이다. 대기 중 종료와 재접속은 전송 계층이 다시 검사한다.
            byte[] response = await transport.ExchangeAsync(request, responseBoundary, timeout,
                reply => ClassifyReadResponse(reply, request, count), cancellationToken).ConfigureAwait(false);
            OperationResult status = McBinaryResponseParser.CheckResponseContentHelper(response);
            if (!status.IsSuccess)
                return OperationResult.CreateFailedResult<ushort[]>(status);
            return OperationResult.CreateSuccessResult(
                byteTransform.ReadUInt16(response, MelsecMc3EBinaryFrame.ResponseDataOffset, count));
        }

        private static ResponseDisposition ClassifyReadResponse(byte[] response, byte[] request, ushort count)
        {
            const int dataOffset = MelsecMc3EBinaryFrame.ResponseDataOffset;
            if (response == null || response.Length < dataOffset || response[0] != 0xD0 || response[1] != 0)
                return ResponseDisposition.Reject;
            int declaredLength = response[7] | (response[8] << 8);
            if (declaredLength != response.Length - MelsecMc3EBinaryFrame.HeaderLength)
                return ResponseDisposition.Reject;
            // 3E에는 요청 일련번호가 없다. 접근 경로를 대조하고 한 요청씩 교환한다.
            for (int index = 2; index <= 6; index++)
                if (response[index] != request[index])
                    return ResponseDisposition.Reject;
            bool deviceError = response[9] != 0 || response[10] != 0;
            // 장치 오류의 부가 정보 길이는 정상 읽기 데이터의 길이와 구분한다.
            return deviceError || response.Length == dataOffset + count * 2
                ? ResponseDisposition.Accept : ResponseDisposition.Reject;
        }
    }
}
