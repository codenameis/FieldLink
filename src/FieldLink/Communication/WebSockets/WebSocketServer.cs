using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.WebSockets
{
    /// <summary>인증된 WebSocket 요청을 연결별 처리 함수에 전달합니다.</summary>
    public sealed class WebSocketServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly string prefix;
        private readonly string path;
        private readonly Func<HttpListenerRequest, bool> authorize;
        private readonly Func<WebSocketConnection, CancellationToken, Task> handler;
        private readonly int maximumMessageBytes;
        private readonly int maximumConnections;
        private readonly TimeSpan sendTimeout;
        private readonly SemaphoreSlim broadcastGate = new SemaphoreSlim(1, 1);
        private Run current;
        private bool disposed;
        private Exception lastConnectionError;

        /// <summary>ws/wss 주소, 업그레이드 전 접근 판정, 연결 처리 함수를 지정합니다.</summary>
        public WebSocketServer(Uri address, Func<HttpListenerRequest, bool> authorize,
            Func<WebSocketConnection, CancellationToken, Task> handler,
            int maximumMessageBytes = 1024 * 1024, int maximumConnections = 100, TimeSpan? sendTimeout = null)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (!address.IsAbsoluteUri || (address.Scheme != "ws" && address.Scheme != "wss") ||
                !address.AbsolutePath.EndsWith("/", StringComparison.Ordinal) || address.Query.Length != 0 ||
                address.Fragment.Length != 0 || address.UserInfo.Length != 0)
                throw new ArgumentException("쿼리 없는 ws/wss 디렉터리 주소를 지정하세요.", nameof(address));
            this.authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (maximumMessageBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumMessageBytes));
            if (maximumConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumConnections));
            this.sendTimeout = sendTimeout ?? TimeSpan.FromSeconds(10);
            if (this.sendTimeout.TotalMilliseconds < 1 || this.sendTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sendTimeout));
            this.maximumMessageBytes = maximumMessageBytes;
            this.maximumConnections = maximumConnections;
            var httpAddress = new UriBuilder(address) { Scheme = address.Scheme == "wss" ? "https" : "http" };
            prefix = httpAddress.Uri.AbsoluteUri;
            path = address.AbsolutePath;
        }

        /// <inheritdoc />
        public bool IsRunning { get { lock (sync) return current != null && !current.Stopping && !current.Completion.IsCompleted; } }
        /// <inheritdoc />
        public Task Completion { get { lock (sync) return current?.Completion ?? Task.CompletedTask; } }
        /// <summary>최근 접속·처리 오류입니다.</summary>
        public Exception LastConnectionError => Volatile.Read(ref lastConnectionError);

        /// <summary>현재 연결된 모든 클라이언트에 텍스트를 보냅니다. 실패한 송신은 반환 Task로 전달합니다.</summary>
        public Task BroadcastTextAsync(string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return BroadcastAsync((peer, token) => peer.SendTextAsync(text, token), cancellationToken);
        }

        /// <summary>현재 연결된 모든 클라이언트에 바이너리를 보냅니다. 작업 완료 전까지 배열을 변경하지 마세요.</summary>
        public Task BroadcastBinaryAsync(byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return BroadcastAsync((peer, token) => peer.SendBinaryAsync(payload, token), cancellationToken);
        }

        private async Task BroadcastAsync(Func<WebSocketConnection, CancellationToken, Task> send, CancellationToken cancellationToken)
        {
            Run run;
            lock (sync)
            {
                if (current == null || current.Stopping)
                    throw new InvalidOperationException("WebSocket 서버가 실행 중이지 않습니다.");
                run = current;
                run.ActiveBroadcasts++;
            }
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, run.Cancellation.Token))
                {
                    await broadcastGate.WaitAsync(linked.Token).ConfigureAwait(false);
                    try
                    {
                        WebSocketConnection[] peers;
                        lock (sync)
                        {
                            if (current != run || run.Stopping)
                                throw new OperationCanceledException(linked.Token);
                            peers = run.Peers.Select(peer => peer.Connection).Where(connection => connection.IsOpen).ToArray();
                        }
                        Task[] sends = peers.Select(async peer => await send(peer, linked.Token).ConfigureAwait(false)).ToArray();
                        await Task.WhenAll(sends).ConfigureAwait(false);
                    }
                    finally { broadcastGate.Release(); }
                }
            }
            finally
            {
                lock (sync)
                    if (--run.ActiveBroadcasts == 0 && run.Stopping)
                        run.BroadcastDrained.TrySetResult(true);
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(WebSocketServer));
                if (current != null)
                    throw new InvalidOperationException("서버가 이미 실행 중이거나 종료 중입니다.");
                var run = new Run();
                run.Listener.Prefixes.Add(prefix);
                try { run.Listener.Start(); }
                catch { run.Listener.Close(); run.Cancellation.Dispose(); throw; }
                current = run;
                run.Completion = Task.Run(() => AcceptAsync(run));
                return Task.CompletedTask;
            }
        }

        private async Task AcceptAsync(Run run)
        {
            try
            {
                while (!run.Stopping)
                {
                    HttpListenerContext context = await run.Listener.GetContextAsync().ConfigureAwait(false);
                    if (run.Stopping)
                    {
                        Reject(context, HttpStatusCode.ServiceUnavailable);
                        break;
                    }
                    await AcceptConnectionAsync(run, context).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (run.Stopping &&
                (error is HttpListenerException || error is ObjectDisposedException || error is InvalidOperationException)) { }
            catch (Exception error) { Volatile.Write(ref lastConnectionError, error); }
            finally
            {
                WebSocketConnection[] connections;
                lock (sync) connections = BeginStop(run);
                SignalStop(run, connections);
                await run.StopSignaled.Task.ConfigureAwait(false);
                await run.BroadcastDrained.Task.ConfigureAwait(false);
                Task[] completions;
                lock (sync)
                    completions = run.Peers.Select(peer => peer.Completion).Where(task => task != null).ToArray();
                try { await Task.WhenAll(completions).ConfigureAwait(false); }
                finally
                {
                    run.Listener.Close();
                    run.Cancellation.Dispose();
                    lock (sync)
                        if (current == run)
                            current = null;
                }
            }
        }

        private async Task AcceptConnectionAsync(Run run, HttpListenerContext context)
        {
            try
            {
                if (context.Request.Url?.AbsolutePath != path || !context.Request.IsWebSocketRequest)
                {
                    Reject(context, HttpStatusCode.BadRequest);
                    return;
                }
                if (!authorize(context.Request))
                {
                    Reject(context, HttpStatusCode.Forbidden);
                    return;
                }
                lock (sync)
                    if (run.Peers.Count >= maximumConnections)
                    {
                        Reject(context, HttpStatusCode.ServiceUnavailable);
                        return;
                    }
                var upgraded = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var connection = new WebSocketConnection(upgraded.WebSocket,
                    context.Request.RemoteEndPoint?.ToString(), maximumMessageBytes, sendTimeout);
                var peer = new Peer(connection);
                lock (sync)
                {
                    if (run.Stopping)
                    {
                        connection.Dispose();
                        return;
                    }
                    run.Peers.Add(peer);
                    peer.Completion = Task.Run(() => HandlePeerAsync(run, peer));
                }
            }
            catch (Exception error)
            {
                Volatile.Write(ref lastConnectionError, error);
                try { Reject(context, HttpStatusCode.InternalServerError); }
                catch (Exception) { }
            }
        }

        private async Task HandlePeerAsync(Run run, Peer peer)
        {
            try { await handler(peer.Connection, run.Cancellation.Token).ConfigureAwait(false); }
            catch (Exception error) when (run.Stopping &&
                (error is OperationCanceledException || error is ObjectDisposedException || error is System.Net.WebSockets.WebSocketException)) { }
            catch (Exception error) { Volatile.Write(ref lastConnectionError, error); }
            finally
            {
                if (!run.Stopping)
                {
                    try { await peer.Connection.CloseAfterHandlerAsync().ConfigureAwait(false); }
                    catch (Exception error)
                    {
                        if (!run.Stopping) Volatile.Write(ref lastConnectionError, error);
                    }
                }
                peer.Connection.Dispose();
                lock (sync) run.Peers.Remove(peer);
            }
        }

        private static void Reject(HttpListenerContext context, HttpStatusCode status)
        {
            context.Response.StatusCode = (int)status;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            WebSocketConnection[] connections;
            lock (sync)
            {
                run = current;
                connections = run == null ? null : BeginStop(run);
            }
            if (run == null)
                return;
            SignalStop(run, connections);
            await run.Completion.ConfigureAwait(false);
        }

        private static WebSocketConnection[] BeginStop(Run run)
        {
            if (run.Stopping) return null;
            run.Stopping = true;
            if (run.ActiveBroadcasts == 0) run.BroadcastDrained.TrySetResult(true);
            return run.Peers.Select(peer => peer.Connection).ToArray();
        }

        private void SignalStop(Run run, WebSocketConnection[] connections)
        {
            if (connections == null) return;
            try
            {
                Cleanup(run.Listener.Stop);
                Cleanup(run.Cancellation.Cancel);
                foreach (WebSocketConnection connection in connections)
                    Cleanup(connection.Dispose);
            }
            finally { run.StopSignaled.TrySetResult(true); }
        }

        private void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) { Volatile.Write(ref lastConnectionError, error); }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Run run;
            WebSocketConnection[] connections;
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                run = current;
                connections = run == null ? null : BeginStop(run);
            }
            if (run != null) SignalStop(run, connections);
        }

        private sealed class Run
        {
            internal readonly HttpListener Listener = new HttpListener();
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal readonly HashSet<Peer> Peers = new HashSet<Peer>();
            internal readonly TaskCompletionSource<bool> StopSignaled =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource<bool> BroadcastDrained =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal int ActiveBroadcasts;
            internal Task Completion = Task.CompletedTask;
            internal volatile bool Stopping;
        }

        private sealed class Peer
        {
            internal Peer(WebSocketConnection connection) { Connection = connection; }
            internal WebSocketConnection Connection { get; }
            internal Task Completion { get; set; }
        }
    }
}
