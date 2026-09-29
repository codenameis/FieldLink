using FieldLink.Communication.WebSockets;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Push
{
    /// <summary>하나의 토픽에 접속해 공정 상태 텍스트를 수신합니다.</summary>
    public sealed class PushClient : IDisposable
    {
        private readonly WebSocketConnection connection;
        private PushClient(WebSocketConnection connection) { this.connection = connection; }
        /// <summary>구독 확인을 받은 후 반환합니다. 재접속과 재전송은 호출자가 결정합니다.</summary>
        public static async Task<PushClient> ConnectAsync(Uri address, string topic,
            int maximumMessageBytes = 1024 * 1024, Action<ClientWebSocketOptions> configure = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            PushTopic.Validate(topic);
            WebSocketConnection connection = await WebSocketClient.ConnectAsync(address,
                maximumMessageBytes: maximumMessageBytes, configure: configure,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            try
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    await connection.SendTextAsync(topic, deadline.Token).ConfigureAwait(false);
                    WebSocketMessage response = await connection.ReceiveAsync(deadline.Token).ConfigureAwait(false);
                    if (response == null || response.Type != WebSocketMessageType.Text || response.Text != "subscribed")
                        throw new InvalidDataException("Push 구독 확인이 올바르지 않습니다.");
                }
                return new PushClient(connection);
            }
            catch { connection.Dispose(); throw; }
        }
        /// <summary>다음 Push 값을 받습니다. 정상 종료는 null입니다.</summary>
        public async Task<string> ReceiveAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            WebSocketMessage message = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (message == null) return null;
            if (message.Type != WebSocketMessageType.Text)
            {
                connection.Dispose();
                throw new InvalidDataException("Push 값은 텍스트여야 합니다.");
            }
            return message.Text;
        }
        /// <summary>연결의 로컬 상태입니다.</summary>
        public bool IsOpen => connection.IsOpen;
        /// <summary>구독을 종료합니다.</summary>
        public Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken)) => connection.CloseAsync(cancellationToken);
        /// <inheritdoc />
        public void Dispose() => connection.Dispose();
    }
}
