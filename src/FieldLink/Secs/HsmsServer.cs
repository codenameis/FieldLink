using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Internal;
using FieldLink.Secs.Types;

namespace FieldLink.Secs
{
    /// <summary>HSMS 연결을 받아 응답·이벤트 전송을 제공하는 서버입니다.</summary>
    public sealed class HsmsServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly IPEndPoint endpoint;
        private readonly ushort deviceID;
        private readonly TimeSpan timeout;
        private readonly int maximum, maximumConnections;
        private readonly Encoding encoding;
        private readonly bool automaticGemReplies;
        private Run current;
        private bool disposed;
        private Exception lastConnectionError;
        /// <summary>수신 주소와 연결별 설정을 지정합니다. 포트 0은 임시 포트를 할당합니다.</summary>
        public HsmsServer(IPEndPoint endpoint, ushort deviceID = 1, TimeSpan? timeout = null,
            int maximumFrameLength = 1024 * 1024, int maximumConnections = 100,
            Encoding stringEncoding = null, bool automaticGemReplies = false)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            this.endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
            this.timeout = timeout ?? TimeSpan.FromSeconds(10);
            ClientOperation.ValidateTimeout(this.timeout);
            if (maximumFrameLength < 14)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            if (maximumConnections < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumConnections));
            this.deviceID = deviceID;
            maximum = maximumFrameLength;
            this.maximumConnections = maximumConnections;
            encoding = stringEncoding ?? Encoding.Default;
            this.automaticGemReplies = automaticGemReplies;
        }
        /// <summary>수신된 비요청 메시지입니다. 세션별 직렬 콜백에서 응답을 보낼 수 있습니다.</summary>
        public event Action<HsmsSession, SecsMessage> MessageReceived;
        /// <summary>현재 열린 세션의 스냅샷입니다.</summary>
        public HsmsSession[] Sessions { get { lock (sync) return current == null ? new HsmsSession[0] : current.Sessions.Where(s => s.IsOpen).ToArray(); } }
        /// <summary>실제 수신 주소입니다.</summary>
        public IPEndPoint LocalEndPoint { get { lock (sync) return current == null ? null : new IPEndPoint(current.Endpoint.Address, current.Endpoint.Port); } }
        /// <summary>마지막 연결 오류입니다. 사용자 콜백 오류는 해당 세션에 있습니다.</summary>
        public Exception LastConnectionError { get { lock (sync) return lastConnectionError; } }
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
                    throw new ObjectDisposedException(nameof(HsmsServer));
                if (current != null)
                    throw new InvalidOperationException("HSMS server is running or stopping.");
                var listener = new TcpListener(endpoint);
                try { listener.Start(); }
                catch { listener.Stop(); throw; }
                var run = new Run(listener);
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
                    var socket = await run.Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    lock (sync)
                    {
                        if (run.Stopping || run.Sessions.Count >= maximumConnections)
                        {
                            socket.Dispose();
                            continue;
                        }
                        HsmsSession session;
                        try { session = new HsmsSession(socket, deviceID, timeout, maximum, encoding, automaticGemReplies); }
                        catch { socket.Dispose(); throw; }
                        session.MessageReceived += (sender, message) => MessageReceived?.Invoke(sender, message);
                        run.Sessions.Add(session);
                        session.Start();
                        _ = ObserveSessionAsync(run, session);
                    }
                }
            }
            catch (Exception) when (run.Stopping) { } // Listener.Stop interrupts pending Accept.
            finally
            {
                HsmsSession[] sessions = SignalStop(run);
                await run.StopSignaled.Task.ConfigureAwait(false);
                await Task.WhenAll(sessions.Select(s => s.Completion)).ConfigureAwait(false);
            }
        }
        private async Task ObserveSessionAsync(Run run, HsmsSession session)
        {
            // Completion is non-faulting; LastError preserves the receive/transport failure.
            await session.Completion.ConfigureAwait(false);
            lock (sync)
            {
                run.Sessions.Remove(session);
                if (!run.Stopping && session.LastError != null)
                    lastConnectionError = session.LastError;
            }
        }
        /// <summary>현재 세션 스냅샷에 W=0 메시지를 전송합니다. 일부 전송 후 실패할 수 있으며 재전송하지 않습니다.</summary>
        public async Task PublishAsync(byte stream, byte function, SecsValue data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream > 127)
                throw new ArgumentOutOfRangeException(nameof(stream));
            cancellationToken.ThrowIfCancellationRequested();
            HsmsSession[] sessions;
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(HsmsServer));
                if (current == null || current.Stopping)
                    throw new InvalidOperationException("Start the HSMS server before publishing.");
                sessions = current.Sessions.ToArray();
            }
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                byte[] body = (data ?? new SecsValue()).ToSourceBytes(encoding);
                if (body.Length > maximum - 14)
                    throw new ArgumentOutOfRangeException(nameof(data), "HSMS frame exceeds its configured maximum.");
                try
                {
                    foreach (var session in sessions)
                        await session.SendBytesAsync(stream, function, body, false, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("HSMS publish exceeded its total deadline.");
                }
            }
        }
        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            lock (sync)
                run = current;
            if (run == null)
                return;
            SignalStop(run);
            try { await run.Completion.ConfigureAwait(false); }
            finally
            {
                lock (sync)
                    if (ReferenceEquals(current, run))
                        current = null;
            }
        }
        private HsmsSession[] SignalStop(Run run)
        {
            HsmsSession[] sessions;
            lock (sync)
            {
                if (run.Stopping)
                    return run.StoppedSessions;
                run.Stopping = true;
                sessions = run.Sessions.ToArray();
                run.StoppedSessions = sessions;
            }
            try
            {
                run.Listener.Stop();
                foreach (var session in sessions)
                    session.Dispose();
            }
            finally { run.StopSignaled.TrySetResult(true); }
            return sessions;
        }
        /// <inheritdoc />
        public void Dispose()
        {
            Run run;
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                run = current;
            }
            if (run != null)
                SignalStop(run);
        }
        private sealed class Run
        {
            internal readonly TcpListener Listener;
            internal readonly IPEndPoint Endpoint;
            internal readonly HashSet<HsmsSession> Sessions = new HashSet<HsmsSession>();
            internal volatile bool Stopping;
            internal HsmsSession[] StoppedSessions;
            internal readonly TaskCompletionSource<bool> StopSignaled =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Completion = Task.CompletedTask;
            internal Run(TcpListener listener) { Listener = listener; Endpoint = (IPEndPoint)listener.LocalEndpoint; }
        }
    }
}
