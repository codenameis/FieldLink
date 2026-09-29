using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Udp
{
    /// <summary>UDP 데이터그램을 순서대로 콜백에 전달하는 서버입니다. 장치별 상대 목록은 보관하지 않습니다.</summary>
    public sealed class UdpServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly IPEndPoint endpoint;
        private readonly Func<byte[], IPEndPoint, Task> handler;
        private readonly int maximumDatagramLength;
        private Run current;
        private bool disposed;
        private long generation;
        /// <summary>로컬 주소, 수신 콜백, 허용하는 데이터그램 길이를 지정합니다.</summary>
        public UdpServer(IPEndPoint endpoint, Func<byte[], IPEndPoint, Task> handler, int maximumDatagramLength = 65507)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            this.endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            if (maximumDatagramLength < 1 || maximumDatagramLength > 65507)
                throw new ArgumentOutOfRangeException(nameof(maximumDatagramLength));
            this.maximumDatagramLength = maximumDatagramLength;
        }
        /// <summary>성공적인 시작마다 증가합니다. 상대 장치 목록을 이 세대에 종속시키세요.</summary>
        public long Generation { get { lock (sync) return generation; } }
        /// <summary>실제로 바인딩한 주소입니다.</summary>
        public IPEndPoint LocalEndPoint { get { lock (sync) return current?.Endpoint; } }
        /// <inheritdoc />
        public bool IsRunning { get { lock (sync) return current != null && !current.Stopped && !current.Completion.IsCompleted; } }
        /// <inheritdoc />
        public Task Completion { get { lock (sync) return current?.Completion ?? Task.CompletedTask; } }
        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(UdpServer));
                if (current != null)
                    throw new InvalidOperationException("서버가 이미 실행 중이거나 종료 중입니다.");
                var run = new Run(new System.Net.Sockets.UdpClient(endpoint));
                current = run; generation++;
                run.Completion = Task.Run(() => ReceiveAsync(run));
                return Task.CompletedTask;
            }
        }
        private async Task ReceiveAsync(Run run)
        {
            try
            {
                while (!run.Stopped)
                {
                    var result = await run.Socket.ReceiveAsync().ConfigureAwait(false);
                    if (run.Stopped)
                        break;
                    if (result.Buffer.Length > maximumDatagramLength)
                        continue;
                    await handler(result.Buffer, result.RemoteEndPoint).ConfigureAwait(false);
                }
            }
            catch (Exception) when (run.Stopped) { }
            finally { lock (sync) Stop(run); }
        }
        /// <summary>현재 서버 소켓으로 한 데이터그램을 전송합니다. 종료·재시작 시 새 소켓으로 재전송하지 않습니다.</summary>
        public async Task SendAsync(byte[] message, IPEndPoint remote, long? expectedGeneration = null)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (remote == null)
                throw new ArgumentNullException(nameof(remote));
            if (message.Length > 65507)
                throw new ArgumentOutOfRangeException(nameof(message));
            Run run;
            lock (sync)
            {
                run = current;
                if (run == null || run.Stopped)
                    throw new InvalidOperationException("UDP 서버가 실행 중이 아닙니다.");
                if (expectedGeneration.HasValue && expectedGeneration.Value != generation)
                    throw new InvalidOperationException("대상 목록을 얻은 UDP 서버가 교체되었습니다.");
            }
            await run.SendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (run.Stopped)
                    throw new InvalidOperationException("전송을 시작한 UDP 서버가 종료되었습니다.");
                await run.Socket.SendAsync(message, message.Length, remote).ConfigureAwait(false);
            }
            finally { run.SendGate.Release(); }
        }
        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            lock (sync)
            {
                run = current;
                if (run == null)
                    return;
                Stop(run);
            }

            try
            {
                await run.Completion.ConfigureAwait(false);
            }
            finally
            {
                lock (sync)
                    if (ReferenceEquals(current, run))
                        current = null;
            }
        }
        private static void Stop(Run run) { if (!run.Stopped) { run.Stopped = true; run.Socket.Dispose(); } }
        /// <summary>소켓을 즉시 닫습니다. 콜백 종료까지 확인하려면 먼저 StopAsync를 기다리세요.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                if (current != null)
                    Stop(current);
            }
        }
        private sealed class Run
        {
            internal Run(System.Net.Sockets.UdpClient socket) { Socket = socket; Endpoint = (IPEndPoint)socket.Client.LocalEndPoint; }
            internal readonly System.Net.Sockets.UdpClient Socket;
            internal readonly IPEndPoint Endpoint;
            internal readonly SemaphoreSlim SendGate = new SemaphoreSlim(1, 1);
            internal volatile bool Stopped;
            internal Task Completion = Task.CompletedTask;
        }
    }
}
