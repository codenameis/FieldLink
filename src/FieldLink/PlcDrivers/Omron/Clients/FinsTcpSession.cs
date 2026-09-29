using FieldLink.Communication;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Omron;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Omron.Clients
{
    /// <summary>FINS/TCP 노드 주소를 연결마다 협상하고 해당 세션에서 FINS 명령을 교환합니다.</summary>
    /// <remarks>
    /// 전송 객체의 OpenAsync·Close·Dispose는 호출자가 담당합니다. 전송 하나에 세션 객체 하나를 사용하세요.
    /// 자동 노드 할당과 직접 연결된 노드의 기본 네트워크·유닛 주소 0을 사용합니다.
    /// 주소 기반 읽기·쓰기와 분할 처리는 이 클래스의 범위가 아닙니다. 기존 빌더의 명령 본문을 전달하세요.
    /// 재접속 후 이전 노드 주소와 SID를 사용하지 않으며, 명령을 자동 재전송하지 않습니다.
    /// </remarks>
    public sealed class FinsTcpSession
    {
        private static readonly FinsTcpFrame Frame = new FinsTcpFrame();
        private readonly TcpProtocolSession<FinsTcpFrameOptions> session;
        private readonly TimeSpan timeout;

        /// <summary>호출자가 소유한 전송과 대기·협상·명령 전체의 제한 시간을 지정합니다.</summary>
        /// <param name="transport">명시적으로 OpenAsync를 완료한 뒤 사용할 TCP 전송입니다.</param>
        /// <param name="timeout">기본값은 3초입니다. 1~Int32.MaxValue 밀리초를 허용합니다.</param>
        public FinsTcpSession(ITcpClient transport, TimeSpan? timeout = null)
        {
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout < TimeSpan.FromMilliseconds(1) || this.timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            session = new TcpProtocolSession<FinsTcpFrameOptions>(transport, NegotiateAsync);
        }

        /// <summary>현재 열린 TCP 연결에서 노드 주소 협상이 완료되었는지 조회합니다.</summary>
        public bool IsInitialized => session.IsInitialized;

        /// <summary>현재 연결의 노드 주소를 협상합니다. 이미 초기화한 같은 연결에서는 메시지를 보내지 않습니다.</summary>
        /// <param name="cancellationToken">호출자 취소입니다.</param>
        /// <returns>협상 결과입니다. 장치가 거절하면 코드가 보존되고 연결은 폐기됩니다.</returns>
        public async Task<OperationResult> InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteAsync((_, __) => Task.FromResult(OperationResult.CreateSuccessResult(true)),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>필요한 노드 협상 후 FINS 명령 본문을 보내고 응답을 해석합니다.</summary>
        /// <param name="command">MRC·SRC로 시작하는 2~2002바이트 본문입니다. TCP·FINS 헤더는 포함하지 않습니다.</param>
        /// <param name="cancellationToken">호출자 취소입니다.</param>
        /// <returns>기존 FINS 파서가 해석한 데이터 또는 장치 오류입니다. 명령별 데이터 길이 검사는 호출자가 수행합니다.</returns>
        /// <remarks>
        /// 반환 Task가 끝날 때까지 command를 수정하거나 재사용하지 마세요. 명령 거절은 연결을 유지합니다.
        /// 세션 협상 거절·TCP 오류 알림은 연결을 폐기하고 오류 결과를 반환합니다.
        /// 잘못된 응답·취소·타임아웃은 전송 예외를 유지하며, 다음 명령 전에 명시적 재접속이 필요합니다.
        /// </remarks>
        public Task<OperationResult<byte[]>> ExchangeAsync(byte[] command,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (command.Length < 2 || command.Length > 2002)
                throw new ArgumentOutOfRangeException(nameof(command), "FINS 명령 본문은 2~2002바이트여야 합니다.");
            return ExecuteAsync((transaction, options) => ExchangeCoreAsync(transaction, options, command), cancellationToken);
        }

        internal static async Task<OperationResult<byte[]>> ExchangeCoreAsync(ITcpTransaction transaction,
            FinsTcpFrameOptions options, byte[] command, int? expectedBytes = null, bool bitData = false)
        {
            byte[] request = OmronFinsNetCommandBuilder.PackCommand(options, command);
            options.SID = unchecked((byte)(options.SID + 1));
            byte[] response = await transaction.ExchangeAsync(request, Frame, reply =>
            {
                var disposition = ClassifyResponse(reply, request);
                if (disposition != ResponseDisposition.Accept || ReadInt32(reply, 8) == 3 || !expectedBytes.HasValue)
                    return disposition;
                var parsed = OmronFinsNetResponseParser.ResponseValidAnalysis(reply);
                if (!parsed.IsSuccess)
                {
                    // 정상 end code의 빈 읽기 응답은 장치 거절이 아니라 잘못된 프레임이다.
                    bool commandError = (reply[28] & 0x7F) != 0 || (reply[28] & 0x80) != 0 || (reply[29] & 0x3F) != 0;
                    return commandError ? ResponseDisposition.Accept : ResponseDisposition.Reject;
                }
                if (parsed.Content.Length != expectedBytes.Value)
                    return ResponseDisposition.Reject;
                if (bitData)
                    foreach (byte value in parsed.Content)
                        if (value > 1)
                            return ResponseDisposition.Reject;
                return ResponseDisposition.Accept;
            }).ConfigureAwait(false);
            if (ReadInt32(response, 8) == 3)
                throw new SessionRejectedException(ReadInt32(response, 12));
            return OmronFinsNetResponseParser.ResponseValidAnalysis(response);
        }

        internal async Task<OperationResult<T>> ExecuteAsync<T>(
            Func<ITcpTransaction, FinsTcpFrameOptions, Task<OperationResult<T>>> command, CancellationToken token,
            TimeSpan? operationTimeout = null)
        {
            try { return await session.ExecuteAsync(command, operationTimeout ?? timeout, token).ConfigureAwait(false); }
            catch (SessionRejectedException error)
            {
                // 트랜잭션이 자신이 사용한 연결을 폐기한 뒤에 결과로 바꾼다.
                // 여기서 transport.Close()를 호출하면 경합 중 새 연결까지 닫을 수 있다.
                return new OperationResult<T>(error.ErrorCode, error.Message)
                { FailureDetails = new OperationFailureDetails(null, "Session", null, 0, 0, error) };
            }
        }

        private static async Task<FinsTcpFrameOptions> NegotiateAsync(ITcpTransaction transaction)
        {
            byte[] response = await transaction.ExchangeAsync(FinsSessionCommandBuilder.BuildNodeAddressRequest(), Frame)
                .ConfigureAwait(false);
            if (ReadInt32(response, 8) != 1)
                throw new InvalidDataException("FINS/TCP 노드 주소 응답의 명령 코드는 1이어야 합니다.");
            int error = ReadInt32(response, 12);
            if (error != 0)
                throw new SessionRejectedException(error);
            if (response.Length != 24 || !IsNodeAddress(response, 16) || !IsNodeAddress(response, 20))
                throw new InvalidDataException("FINS/TCP 노드 주소 응답에는 1~254 범위의 두 노드 주소가 필요합니다.");
            var options = new FinsTcpFrameOptions();
            // 매 연결마다 새 옵션을 만들어 주소·SID와 세션 수명을 함께 묶는다.
            FinsSessionResponseParser.ParseNodeAddresses(options, response);
            options.SID = 0;
            return options;
        }

        private static bool IsNodeAddress(byte[] response, int offset)
        {
            int node = ReadInt32(response, offset);
            return node >= 1 && node <= 254;
        }

        private static ResponseDisposition ClassifyResponse(byte[] response, byte[] request)
        {
            int command = ReadInt32(response, 8);
            // W421 §7-4-2: 연결 확인 알림은 버리고 다음 응답을 기다린다.
            if (command == 6 && response.Length == 16)
                return ResponseDisposition.Ignore;
            if (command == 3 && response.Length == 16)
                return ResponseDisposition.Accept;
            if (command != 2 || response.Length < 30 || ReadInt32(response, 12) != 0 || (response[16] & 0x40) == 0)
                return ResponseDisposition.Reject;
            for (int i = 0; i < 3; i++)
                if (response[19 + i] != request[22 + i] || response[22 + i] != request[19 + i])
                    return ResponseDisposition.Reject;
            if (response[25] != request[25])
                return ResponseDisposition.Ignore;
            return response[26] == request[26] && response[27] == request[27]
                ? ResponseDisposition.Accept : ResponseDisposition.Reject;
        }

        private static int ReadInt32(byte[] data, int offset) => unchecked((int)(
            ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3]));

        private sealed class SessionRejectedException : Exception
        {
            internal SessionRejectedException(int code)
                : base("FINS/TCP 세션 오류: " + OmronFinsNetResponseParser.GetStatusDescription(code)) { ErrorCode = code; }
            internal int ErrorCode { get; }
        }
    }
}
