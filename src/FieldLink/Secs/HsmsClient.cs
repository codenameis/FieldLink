using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Internal;
using FieldLink.Secs.Types;
using SocketClient = System.Net.Sockets.TcpClient;

namespace FieldLink.Secs
{
    /// <summary>명시적으로 연결하는 HSMS TCP 클라이언트입니다. 자동 재접속·재전송하지 않습니다.</summary>
    public sealed class HsmsClient : ISecs, IDisposable
    {
        private readonly object sync = new object();
        private readonly IPEndPoint endpoint;
        private readonly ushort deviceID;
        private readonly TimeSpan timeout;
        private readonly int maximum;
        private readonly Encoding encoding;
        private readonly bool selectOnOpen, automaticGemReplies;
        private SocketClient opening;
        private HsmsSession current;
        private bool ready, disposed;
        private long generation;
        /// <summary>주소, 장치 ID, 전체 요청 제한 시간과 선택적 Select 초기화를 지정합니다.</summary>
        public HsmsClient(IPEndPoint endpoint, ushort deviceID = 0, TimeSpan? timeout = null,
            bool selectOnOpen = false, int maximumFrameLength = 1024 * 1024,
            Encoding stringEncoding = null, bool automaticGemReplies = true)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (endpoint.Port == 0)
                throw new ArgumentOutOfRangeException(nameof(endpoint));
            this.endpoint = new IPEndPoint(endpoint.Address, endpoint.Port);
            this.timeout = timeout ?? TimeSpan.FromSeconds(10);
            ClientOperation.ValidateTimeout(this.timeout);
            if (maximumFrameLength < 14)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            this.deviceID = deviceID;
            this.selectOnOpen = selectOnOpen;
            maximum = maximumFrameLength;
            encoding = stringEncoding ?? Encoding.Default;
            this.automaticGemReplies = automaticGemReplies;
        }
        /// <summary>Open이 완료된 현재 세션입니다. 이전 세션 객체는 재접속 후에도 재사용할 수 없습니다.</summary>
        public HsmsSession Session
        {
            get
            {
                lock (sync)
                {
                    if (disposed)
                        throw new ObjectDisposedException(nameof(HsmsClient));
                    if (!ready || current == null || !current.IsOpen)
                        throw new InvalidOperationException("Complete OpenAsync before using the HSMS session.");
                    return current;
                }
            }
        }
        /// <summary>명시적 연결과 선택한 초기화가 완료되었는지 표시합니다.</summary>
        public bool IsOpen { get { lock (sync) return ready && current != null && current.IsOpen; } }
        /// <summary>성공한 Open마다 증가합니다.</summary>
        public long ConnectionGeneration { get { lock (sync) return generation; } }
        /// <summary>현재 세션의 GEM 명령입니다.</summary>
        public Gem Gem => Session.Gem;
        /// <summary>장비 비요청 메시지입니다. 세션 콜백 큐에서 실행됩니다.</summary>
        public event Action<HsmsSession, SecsMessage> MessageReceived;
        /// <summary>TCP 연결과 선택적 Select를 하나의 제한 시간 안에서 완료합니다.</summary>
        public async Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SocketClient socket;
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(HsmsClient));
                if (opening != null || (current != null && current.IsOpen))
                    throw new InvalidOperationException("HSMS client is already open or opening.");
                socket = new SocketClient(endpoint.AddressFamily);
                opening = socket;
                ready = false;
            }
            HsmsSession session = null;
            bool connected = false;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                try
                {
                    using (deadline.Token.Register(socket.Dispose))
                    {
                        await socket.ConnectAsync(endpoint.Address, endpoint.Port).ConfigureAwait(false);
                        connected = true;
                        deadline.Token.ThrowIfCancellationRequested();
                        lock (sync)
                        {
                            if (disposed || !ReferenceEquals(opening, socket))
                                throw new OperationCanceledException("HSMS Open was closed.");
                            session = new HsmsSession(socket, deviceID, timeout, maximum, encoding, automaticGemReplies);
                            session.MessageReceived += (sender, message) => MessageReceived?.Invoke(sender, message);
                            current = session;
                            session.Start();
                        }
                        if (selectOnOpen)
                            await session.SelectAsync(deadline.Token).ConfigureAwait(false);
                    }
                    deadline.Token.ThrowIfCancellationRequested();
                    lock (sync)
                    {
                        if (disposed || !ReferenceEquals(opening, socket) || !session.IsOpen)
                            throw new OperationCanceledException("HSMS Open was closed.");
                        ready = true;
                        generation++;
                        opening = null;
                    }
                }
                catch (Exception error)
                {
                    session?.Dispose();
                    socket.Dispose();
                    lock (sync)
                    {
                        if (ReferenceEquals(opening, socket))
                            opening = null;
                        if (ReferenceEquals(current, session))
                        {
                            current = null;
                            ready = false;
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (deadline.IsCancellationRequested)
                        throw new TimeoutException("HSMS Open exceeded its total deadline.", error);
                    if (!connected && !(error is OperationCanceledException))
                        throw new CommunicationException(CommunicationFailure.SocketError, CommunicationStage.Connecting, endpoint.ToString(), 0, 0, error);
                    throw;
                }
            }
        }
        /// <inheritdoc />
        public Task<SecsMessage> RequestAsync(byte stream, byte function, SecsValue data,
            CancellationToken cancellationToken = default(CancellationToken)) =>
            Session.RequestAsync(stream, function, data, cancellationToken);
        /// <summary>현재 연결에 Separate를 보내고 정리합니다. 진행 중인 Open도 중단합니다.</summary>
        public async Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            HsmsSession session;
            SocketClient socket;
            lock (sync)
            {
                session = current; socket = opening;
                current = null; opening = null; ready = false;
            }
            socket?.Dispose();
            if (session != null)
                await session.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        /// <summary>연결과 진행 중인 Open을 즉시 종료합니다.</summary>
        public void Dispose()
        {
            HsmsSession session;
            SocketClient socket;
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                session = current; socket = opening;
                current = null; opening = null; ready = false;
            }
            socket?.Dispose();
            session?.Dispose();
        }
    }
}
