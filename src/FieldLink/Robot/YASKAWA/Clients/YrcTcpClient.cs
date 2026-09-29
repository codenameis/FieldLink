using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.YASKAWA.Protocols;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.YASKAWA.Clients
{
    /// <summary>YRC TCP 협상·헤더 승인·본문 교환을 같은 연결에서 수행합니다. 재접속마다 새 협상을 수행합니다.</summary>
    /// <remarks>전송은 호출자가 열고 닫습니다. 지속 연결을 지원하지 않는 장치는 명령 완료 후 연결을 닫으므로 다음 명령 전에 다시 열어야 합니다.</remarks>
    public sealed class YrcTcpClient
    {
        private readonly TcpProtocolSession<Session> session;
        private readonly TimeSpan timeout;
        private static readonly IFrameBoundary Line = new DelimitedFrame(new byte[] { 13, 10 });
        private static readonly IFrameBoundary Body = new DelimitedFrame(new byte[] { 13 });
        /// <summary>호출자가 소유한 TCP 전송과 협상·명령 전체의 제한 시간을 지정합니다.</summary>
        public YrcTcpClient(ITcpClient transport, TimeSpan? timeout = null)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout);
            session = new TcpProtocolSession<Session>(transport, async tx =>
            {
                string response = Encoding.Default.GetString(await tx.ExchangeAsync(YrcTcpProtocol.BuildConnect(), Line).ConfigureAwait(false));
                var result = YrcTcpProtocol.ParseConnect(response);
                if (!result.IsSuccess)
                    throw new InvalidDataException("YRC 연결 협상 실패: " + result.Message);
                return new Session(result.Content);
            });
        }
        /// <summary>현재 연결에 유효한 협상이 있는지 나타냅니다.</summary>
        public bool IsInitialized => session.IsInitialized;
        /// <summary>요청 빌더의 모든 YRC TCP 명령을 실행하고 지정된 형식으로 응답을 해석합니다.</summary>
        public Task<OperationResult<T>> ExecuteAsync<T>(YrcTcpRequest<T> request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            byte[] header = request.BuildHeader(), body = request.BuildBody();
            return session.ExecuteAsync(async (tx, value) =>
            {
                if (!value.Persistent)
                    tx.CloseAfterCompletion();
                string approval = Encoding.Default.GetString(await tx.ExchangeAsync(header, Line).ConfigureAwait(false));
                var checkedHeader = YrcTcpProtocol.CheckHeaderResponse(approval);
                if (!checkedHeader.IsSuccess)
                    return OperationResult.CreateFailedResult<T>(checkedHeader);
                if (body.Length != 0)
                    await tx.SendAsync(body).ConfigureAwait(false);
                string response = Encoding.ASCII.GetString(await tx.ReceiveAsync(Body).ConfigureAwait(false));
                if (YrcTcpProtocol.RequiresTrailingLf(response))
                    await tx.ReceiveAsync(new FixedLengthFrame(1)).ConfigureAwait(false);
                return request.ParseResponse(response);
            }, timeout, cancellationToken);
        }
        /// <summary>명령·매개 변수 문자열을 해석하여 실행합니다.</summary>
        public Task<OperationResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(YrcTcpRequestBuilder.BuildReadString(address ?? throw new ArgumentNullException(nameof(address))), cancellationToken);
        /// <summary>직접 명령과 본문을 지정합니다.</summary>
        public Task<OperationResult<string>> ReadByCommandAsync(string command, string data = null, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(YrcTcpRequestBuilder.BuildCommand(command ?? throw new ArgumentNullException(nameof(command)), data), cancellationToken);
        private sealed class Session
        {
            internal Session(bool persistent) { Persistent = persistent; }
            internal bool Persistent { get; }
        }
    }
}
