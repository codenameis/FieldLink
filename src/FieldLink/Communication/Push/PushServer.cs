using FieldLink.Communication.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Push
{
    /// <summary>WebSocket 연결 하나에 토픽 하나를 할당하는 공정 상태 Push 서버입니다.</summary>
    public sealed class PushServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, HashSet<WebSocketConnection>> subscribers =
            new Dictionary<string, HashSet<WebSocketConnection>>(StringComparer.Ordinal);
        private readonly SemaphoreSlim publishGate = new SemaphoreSlim(1, 1);
        private readonly WebSocketServer server;

        /// <summary>접근 판정 함수는 업그레이드 전에 실행됩니다.</summary>
        public PushServer(Uri address, Func<HttpListenerRequest, bool> authorize,
            int maximumMessageBytes = 1024 * 1024, int maximumConnections = 100,
            TimeSpan? sendTimeout = null)
        {
            server = new WebSocketServer(address, authorize, HandleAsync,
                maximumMessageBytes, maximumConnections, sendTimeout);
        }

        /// <inheritdoc />
        public bool IsRunning => server.IsRunning;
        /// <inheritdoc />
        public Task Completion => server.Completion;
        /// <summary>최근 접속·처리 오류입니다.</summary>
        public Exception LastConnectionError => server.LastConnectionError;
        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken)) => server.StartAsync(cancellationToken);
        /// <inheritdoc />
        public Task StopAsync() => server.StopAsync();
        /// <inheritdoc />
        public void Dispose() => server.Dispose();

        private async Task HandleAsync(WebSocketConnection connection, CancellationToken stop)
        {
            string topic = null;
            try
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(10));
                    WebSocketMessage hello = await connection.ReceiveAsync(deadline.Token).ConfigureAwait(false);
                    if (hello == null || hello.Type != WebSocketMessageType.Text)
                        return;
                    topic = hello.Text;
                    try { PushTopic.Validate(topic); }
                    catch (ArgumentException) { return; }
                }
                await publishGate.WaitAsync(stop).ConfigureAwait(false);
                try
                {
                    await connection.SendTextAsync("subscribed", stop).ConfigureAwait(false);
                    lock (sync)
                    {
                        if (!subscribers.TryGetValue(topic, out HashSet<WebSocketConnection> group))
                            subscribers.Add(topic, group = new HashSet<WebSocketConnection>());
                        group.Add(connection);
                    }
                }
                finally { publishGate.Release(); }
                while (await connection.ReceiveAsync(stop).ConfigureAwait(false) != null)
                    throw new InvalidOperationException("Push 연결에서는 구독 후 메시지를 보낼 수 없습니다.");
            }
            finally
            {
                if (topic != null)
                    lock (sync)
                        if (subscribers.TryGetValue(topic, out HashSet<WebSocketConnection> group))
                        {
                            group.Remove(connection);
                            if (group.Count == 0) subscribers.Remove(topic);
                        }
            }
        }

        /// <summary>현재 토픽 구독자에게 텍스트를 보냅니다. 한 명이 실패해도 다른 구독자는 시도합니다.</summary>
        public async Task PublishAsync(string topic, string content, CancellationToken cancellationToken = default(CancellationToken))
        {
            PushTopic.Validate(topic);
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!IsRunning) throw new InvalidOperationException("Push 서버가 실행 중이지 않습니다.");
            await publishGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                WebSocketConnection[] targets;
                lock (sync)
                    targets = subscribers.TryGetValue(topic, out HashSet<WebSocketConnection> group)
                        ? group.Where(connection => connection.IsOpen).ToArray()
                        : Array.Empty<WebSocketConnection>();
                await Task.WhenAll(targets.Select(connection => connection.SendTextAsync(content, cancellationToken)))
                    .ConfigureAwait(false);
            }
            finally { publishGate.Release(); }
        }
    }
}
