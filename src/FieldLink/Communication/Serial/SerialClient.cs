using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Serial
{
    /// <summary>명시적으로 연 포트에서 메시지를 순차 교환합니다. 활성 작업 실패 시 포트를 닫고 자동 재전송하지 않습니다.</summary>
    public sealed partial class SerialClient : ISerialClient
    {
        private readonly object sync = new object();
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly AsyncLocal<bool> inTransaction = new AsyncLocal<bool>();
        private readonly ClientStateNotifier status;
        private readonly SerialPortSettings settings;
        private readonly Func<SerialPortSettings, ISerialPortChannel> portFactory;
        private readonly IFrameBoundary boundary;
        private readonly TimeSpan timeout;
        private readonly int maximumFrameLength;
        private PortSession current;
        private long generation;
        private bool disposed;
        private int closing;
        private int openingWorkers;

        /// <summary>불변 포트 설정, 기본 프레임 경계와 제한 시간을 지정합니다. 팩터리는 열기마다 새 포트를 반환해야 합니다.</summary>
        public SerialClient(SerialPortSettings settings, IFrameBoundary responseBoundary, int maximumFrameLength = 1024 * 1024,
            TimeSpan? timeout = null, Func<SerialPortSettings, ISerialPortChannel> portFactory = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            boundary = responseBoundary ?? throw new ArgumentNullException(nameof(responseBoundary));
            if (maximumFrameLength < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            this.maximumFrameLength = maximumFrameLength;
            this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout);
            this.portFactory = portFactory ?? (value => new SystemSerialPortChannel(value));
            status = new ClientStateNotifier(this);
        }
        /// <inheritdoc />
        public ClientState State => status.LastChange.State;
        /// <inheritdoc />
        public ClientStateChangedEventArgs LastStateChange => status.LastChange;
        /// <inheritdoc />
        public Exception LastNotificationError => status.LastNotificationError;
        /// <summary>최근 포트 해제 중 발생한 예외입니다. 취소 콜백에서는 예외를 전파하지 않고 이곳에 보존합니다.</summary>
        public Exception LastCloseError { get; private set; }
        /// <inheritdoc />
        public long ConnectionGeneration { get { lock (sync) return generation; } }
        /// <inheritdoc />
        public event EventHandler<ClientStateChangedEventArgs> StateChanged
        { add { status.Changed += value; } remove { status.Changed -= value; } }
        /// <inheritdoc />
        public Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken)) => OpenAsync(timeout, cancellationToken);
        /// <inheritdoc />
        public async Task OpenAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            ClientOperation.ValidateTimeout(timeout); cancellationToken.ThrowIfCancellationRequested();
            PortSession session;
            lock (sync)
            {
                ThrowIfDisposed();
                if (current != null || closing != 0 || openingWorkers != 0)
                    throw new InvalidOperationException("포트가 열려 있거나 열기·종료가 진행 중입니다.");
                session = new PortSession(portFactory(settings) ?? throw new InvalidOperationException("포트 팩터리가 null을 반환했습니다."), maximumFrameLength, settings);
                current = session; openingWorkers++; status.Change(ClientState.Opening);
            }
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Closed.Token))
            {
                deadline.CancelAfter(timeout);
                CancellationToken token = deadline.Token;
                using (var interrupt = token.Register(() => ReleasePort(session)))
                {
                    try
                    {
                        Task<bool> opening = Task.Run(() =>
                        {
                            try { token.ThrowIfCancellationRequested(); session.Port.Open(); return true; }
                            finally
                            {
                                // 드라이버가 취소 이후 늦게 Open을 완료해도 열린 핸들을 남기지 않는다.
                                if (token.IsCancellationRequested)
                                    try { session.Port.Dispose(); } catch (Exception error) { LastCloseError = error; }
                                lock (sync) openingWorkers--;
                            }
                        });
                        await SerialTransaction.AwaitCancellationAsync(opening, token).ConfigureAwait(false);
                        interrupt.Dispose();
                        token.ThrowIfCancellationRequested();
                        lock (sync)
                        {
                            if (!ReferenceEquals(current, session))
                                throw new OperationCanceledException(session.Closed.Token);
                            session.Timing.Reset();
                            session.Generation = ++generation; status.Change(ClientState.Open);
                        }
                    }
                    catch (Exception error)
                    {
                        Exception translated = Translate(error, cancellationToken, session, deadline.Token, CommunicationStage.Connecting);
                        Invalidate(session, translated); throw translated;
                    }
                }
            }
        }
        /// <inheritdoc />
        public void Close() => CloseCore(false);
        /// <inheritdoc />
        public void Dispose() => CloseCore(true);
        private void CloseCore(bool disposing)
        {
            PortSession session;
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = disposing; session = current; current = null;
                if (session != null)
                    closing++;
                status.Change(disposing ? ClientState.Disposed : ClientState.Closed);
            }
            if (session != null)
                try { session.Closed.Cancel(); ReleasePort(session); }
                finally { lock (sync) closing--; }
        }
        private void ReleasePort(PortSession session)
        {
            lock (session.ReleaseSync)
            {
                if (session.Released)
                    return;
                try { session.Port.Dispose(); } catch (Exception error) { LastCloseError = error; }
                finally { session.Released = true; }
            }
        }
        private void Invalidate(PortSession session, Exception error)
        {
            lock (sync)
            {
                closing++;
                if (ReferenceEquals(current, session)) { current = null; status.Change(ClientState.Faulted, error); }
            }
            try { session.Closed.Cancel(); ReleasePort(session); }
            finally { lock (sync) closing--; }
        }
        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SerialClient));
        }
        private Exception Translate(Exception error, CancellationToken caller, PortSession session, CancellationToken deadline,
            CommunicationStage stage, long sent = 0, long received = 0)
        {
            CommunicationFailure failure;
            if (disposed)
                failure = CommunicationFailure.Disposed;
            else if (caller.IsCancellationRequested)
                failure = CommunicationFailure.Cancelled;
            else if (session.Closed.IsCancellationRequested)
                failure = CommunicationFailure.ConnectionClosed;
            else if (deadline.IsCancellationRequested || error is TimeoutException)
                failure = CommunicationFailure.Timeout;
            else if (error is CommunicationException)
                return error;
            else if (error is InvalidDataException)
                failure = CommunicationFailure.InvalidFrame;
            else if (error is IOException || error is UnauthorizedAccessException)
                failure = CommunicationFailure.PortError;
            else return error;
            return new CommunicationException(failure, stage, settings.PortName, sent, received, error);
        }
        private sealed class PortSession
        {
            internal readonly ISerialPortChannel Port;
            internal readonly CancellationTokenSource Closed = new CancellationTokenSource();
            internal readonly FrameAccumulator Buffer;
            internal readonly SerialLineTiming Timing;
            internal long Generation;
            internal readonly object ReleaseSync = new object();
            internal bool Released;
            internal PortSession(ISerialPortChannel port, int maximum, SerialPortSettings settings)
            { Port = port; Buffer = new FrameAccumulator(maximum); Timing = new SerialLineTiming(settings); }
        }
    }
}
