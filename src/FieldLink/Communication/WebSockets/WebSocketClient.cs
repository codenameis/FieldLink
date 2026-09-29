using FieldLink.Communication.Diagnostics;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.WebSockets
{
    /// <summary>표준 ws/wss 연결을 명시적으로 열어 WebSocketConnection을 반환합니다.</summary>
    public static class WebSocketClient
    {
        /// <summary>지정 주소에 연결합니다. 시간 초과와 인증 헤더는 호출자가 설정할 수 있습니다.</summary>
        public static async Task<WebSocketConnection> ConnectAsync(Uri address, int maximumMessageBytes = 1024 * 1024,
            TimeSpan? connectTimeout = null, TimeSpan? sendTimeout = null, Action<ClientWebSocketOptions> configure = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (!address.IsAbsoluteUri || (address.Scheme != "ws" && address.Scheme != "wss"))
                throw new ArgumentException("ws 또는 wss 주소를 지정하세요.", nameof(address));
            if (maximumMessageBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumMessageBytes));
            TimeSpan timeout = connectTimeout ?? TimeSpan.FromSeconds(10);
            if (timeout.TotalMilliseconds < 1 || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            TimeSpan writeTimeout = sendTimeout ?? TimeSpan.FromSeconds(10);
            if (writeTimeout.TotalMilliseconds < 1 || writeTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sendTimeout));
            cancellationToken.ThrowIfCancellationRequested();
            var socket = new ClientWebSocket();
            try
            {
                configure?.Invoke(socket.Options);
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(timeout);
                    try { await socket.ConnectAsync(address, deadline.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new CommunicationException(CommunicationFailure.Timeout, CommunicationStage.Connecting,
                            address.ToString(), 0, 0, error);
                    }
                }
                return new WebSocketConnection(socket, address.ToString(), maximumMessageBytes, writeTimeout);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
