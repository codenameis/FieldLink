using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Tcp
{
    /// <summary>연결별 콜백을 실행하는 TCP 서버입니다. 프로토콜 해석은 콜백이 담당합니다.</summary>
    public sealed class TcpServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly IPEndPoint endpoint;
        private readonly Func<ITcpServerConnection, CancellationToken, Task> handler;
        private readonly int maximumFrameLength, maximumConnections;
        private readonly TimeSpan ioTimeout;
        private Run current;
        private bool disposed;
        private Exception lastConnectionError;
        /// <summary>수신 주소, 연결 처리 함수, 프레임·연결 수 제한과 송수신 제한 시간을 지정합니다.</summary>
        public TcpServer(IPEndPoint endpoint, Func<ITcpServerConnection, CancellationToken, Task> handler,
            int maximumFrameLength = 1024 * 1024, int maximumConnections = 100, TimeSpan? ioTimeout = null)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            this.endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (maximumFrameLength < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            if (maximumConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumConnections));
            this.maximumFrameLength = maximumFrameLength; this.maximumConnections = maximumConnections;
            this.ioTimeout = ioTimeout ?? TimeSpan.FromSeconds(30);
            if (this.ioTimeout.TotalMilliseconds < 1 || this.ioTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(ioTimeout));
        }
        /// <summary>실제로 바인딩된 주소입니다. 포트 0으로 시작하면 운영체제가 선택한 포트를 반환합니다.</summary>
        public IPEndPoint LocalEndPoint { get { lock (sync) return current == null ? null : new IPEndPoint(current.Endpoint.Address, current.Endpoint.Port); } }
        /// <summary>마지막 연결 처리 또는 종료 취소 콜백 오류입니다. 연결 정리는 오류와 관계없이 계속합니다.</summary>
        public Exception LastConnectionError => Volatile.Read(ref lastConnectionError);
        /// <inheritdoc />
        public bool IsRunning { get { lock (sync) return current != null && !current.Stopping && !current.Completion.IsCompleted; } }
        /// <inheritdoc />
        public Task Completion { get { lock (sync) return current?.Completion ?? Task.CompletedTask; } }
        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(TcpServer));
                if (current != null)
                    throw new InvalidOperationException("서버가 이미 실행 중이거나 종료 중입니다.");
                var run = new Run(new TcpListener(endpoint));
                try { run.Listener.Start(); }
                catch { run.Listener.Stop(); run.Stop.Dispose(); throw; }
                run.Endpoint = (IPEndPoint)run.Listener.LocalEndpoint;
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
                    System.Net.Sockets.TcpClient socket = await run.Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    lock (sync)
                    {
                        if (run.Stopping || run.Connections.Count >= maximumConnections)
                        {
                            socket.Dispose();
                            continue;
                        }
                        var connection = new TcpServerConnection(socket, maximumFrameLength, ioTimeout, run.Stop.Token);
                        run.Connections.Add(connection);
                        Task worker = Task.Run(() => ServeAsync(run, connection));
                        run.Workers.Add(worker);
                        // 완료된 작업만 제거하여 장시간 실행 서버의 작업 목록이 계속 늘지 않게 합니다.
                        run.Workers.RemoveAll(t => t.IsCompleted);
                    }
                }
            }
            catch (Exception) when (run.Stopping) { } // 종료 중 Listener.Stop이 중단한 Accept이다.
            finally
            {
                Task[] workers;
                TcpServerConnection[] connections;
                lock (sync)
                {
                    connections = BeginStop(run);
                    workers = run.Workers.ToArray();
                }
                SignalStop(run, connections);
                // 다른 Stop/Dispose 호출이 실행 중인 취소 콜백과 소켓 정리까지 완료해야 한다.
                await run.StopSignaled.Task.ConfigureAwait(false);
                try { await Task.WhenAll(workers).ConfigureAwait(false); }
                finally { run.Stop.Dispose(); }
            }
        }
        private async Task ServeAsync(Run run, TcpServerConnection connection)
        {
            try
            {
                await handler(connection, run.Stop.Token).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (!run.Stopping)
                    Volatile.Write(ref lastConnectionError, error);
            }
            finally
            {
                connection.Dispose();
                lock (sync)
                    run.Connections.Remove(connection);
            }
        }
        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            TcpServerConnection[] connections;
            lock (sync)
            {
                run = current;
                if (run == null)
                    return;
                connections = BeginStop(run);
            }
            SignalStop(run, connections);

            try
            {
                await run.Completion.ConfigureAwait(false);
            }
            finally
            {
                lock (sync)
                    if (ReferenceEquals(current, run))
                    {
                        current = null;
                    }
            }
        }
        // sync 안에서는 종료 소유권과 대상을 확정할 뿐, 사용자 코드는 실행하지 않는다.
        private static TcpServerConnection[] BeginStop(Run run)
        {
            if (run.Stopping)
                return null;
            run.Stopping = true;
            return run.Connections.ToArray();
        }
        private void SignalStop(Run run, TcpServerConnection[] connections)
        {
            if (connections == null)
                return;
            try
            {
                // 이전: lock (sync) { SignalStop(run); } 안에서 run.Stop.Cancel()을 호출했다.
                // Cancel은 등록 콜백을 동기 실행하므로 앱 잠금과의 역전을 피하려면 잠금 밖이어야 한다.
                // 리스너부터 닫고, 콜백 등록이 유지된 상태에서 취소한 뒤 기존 연결을 닫는다.
                CleanupAndRecordError(run.Listener.Stop);
                CleanupAndRecordError(run.Stop.Cancel);
                foreach (var connection in connections)
                    CleanupAndRecordError(connection.Dispose);
            }
            finally
            {
                run.StopSignaled.TrySetResult(true);
            }
        }
        private void CleanupAndRecordError(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception error) { Volatile.Write(ref lastConnectionError, error); }
        }
        /// <summary>종료를 요청하고 리스너를 닫습니다. 취소 콜백은 동기 실행되며 반환 후 연결을 정리합니다. 작업자 완료는 StopAsync로 확인하세요.</summary>
        public void Dispose()
        {
            Run run;
            TcpServerConnection[] connections;
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                run = current;
                connections = run == null ? null : BeginStop(run);
            }
            SignalStop(run, connections);
        }
        private sealed class Run
        {
            internal Run(TcpListener listener) { Listener = listener; }
            internal readonly TcpListener Listener;
            internal IPEndPoint Endpoint;
            internal volatile bool Stopping;
            internal readonly CancellationTokenSource Stop = new CancellationTokenSource();
            internal readonly TaskCompletionSource<bool> StopSignaled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly HashSet<TcpServerConnection> Connections = new HashSet<TcpServerConnection>();
            internal readonly List<Task> Workers = new List<Task>();
            internal Task Completion = Task.CompletedTask;
        }
    }
}
