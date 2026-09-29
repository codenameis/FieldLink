using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Tcp;
using FieldLink.Communication.Internal;
using FieldLink.Secs.Protocols;
using FieldLink.Secs.Types;

namespace FieldLink.Secs
{
    /// <summary>연결 하나의 HSMS 요청·응답과 비요청 메시지를 처리합니다. 재접속 시 새 세션을 사용하세요.</summary>
    public sealed class HsmsSession : ISecs, IDisposable
    {
        private readonly object sync = new object();
        private readonly TcpDuplexConnection connection;
        private readonly Dictionary<uint, Pending> pending = new Dictionary<uint, Pending>();
        private readonly Queue<SecsMessage> notifications = new Queue<SecsMessage>();
        private readonly int maximum;
        private readonly TimeSpan timeout;
        private readonly bool automaticGemReplies;
        private uint nextID;
        private bool closed, closing, dispatching, selected;
        private Task closeTask;
        private Exception lastError, lastHandlerError;
        internal HsmsSession(System.Net.Sockets.TcpClient socket, ushort deviceID, TimeSpan timeout,
            int maximum, Encoding encoding, bool automaticGemReplies)
        {
            connection = new TcpDuplexConnection(socket, maximum);
            DeviceID = deviceID;
            this.timeout = timeout;
            this.maximum = maximum;
            StringEncoding = encoding;
            this.automaticGemReplies = automaticGemReplies;
            Gem = new Gem(this, encoding);
        }
        internal void Start() { Completion = Task.Run(ReceiveLoopAsync); }
        /// <summary>세션의 기본 데이터 장치 ID입니다.</summary>
        public ushort DeviceID { get; }
        /// <summary>문자열 인코딩입니다.</summary>
        public Encoding StringEncoding { get; }
        /// <summary>연결된 상대 주소입니다.</summary>
        public EndPoint RemoteEndPoint => connection.RemoteEndPoint;
        /// <summary>이 세션이 아직 닫히지 않았는지 표시합니다.</summary>
        public bool IsOpen { get { lock (sync) return !closed && !closing; } }
        /// <summary>Select 절차를 완료한 상태입니다. 원본 호환 모드에서는 Select 없이 데이터 교환도 허용합니다.</summary>
        public bool IsSelected { get { lock (sync) return selected && !closed && !closing; } }
        /// <summary>수신 루프의 종료입니다. 오류는 LastError에 보존됩니다.</summary>
        public Task Completion { get; private set; } = Task.CompletedTask;
        /// <summary>연결을 종료시킨 마지막 오류입니다.</summary>
        public Exception LastError { get { lock (sync) return lastError; } }
        /// <summary>사용자 메시지 콜백에서 발생한 마지막 오류입니다.</summary>
        public Exception LastMessageHandlerError { get { lock (sync) return lastHandlerError; } }
        /// <summary>자주 쓰는 GEM 명령입니다.</summary>
        public Gem Gem { get; }
        /// <summary>비요청 메시지입니다. 별도 직렬 콜백 큐에서 실행되므로 콜백에서 응답·요청·종료할 수 있습니다.</summary>
        /// <remarks>닫을 때 미실행 콜백은 폐기하고 실행 중인 사용자 콜백의 종료를 기다리지 않습니다.</remarks>
        public event Action<HsmsSession, SecsMessage> MessageReceived;

        /// <inheritdoc />
        public Task<SecsMessage> RequestAsync(byte stream, byte function, SecsValue data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidatePrimary(stream, function);
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            byte[] body = (data ?? new SecsValue()).ToSourceBytes(StringEncoding);
            return RequestBytesAsync(stream, function, body, cancellationToken);
        }
        /// <summary>인코딩된 본문으로 primary를 보냅니다. 본문은 호출 시 복사됩니다.</summary>
        public Task<SecsMessage> RequestBytesAsync(byte stream, byte function, byte[] data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidatePrimary(stream, function);
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            return ExchangeAsync(new SecsMessage(DeviceID, stream, function, NextID(), data, true),
                0, (byte)(function + 1), cancellationToken);
        }
        /// <summary>응답을 기다리지 않고 전송합니다. 장비 처리 성공을 의미하지 않습니다.</summary>
        public Task SendAsync(byte stream, byte function, SecsValue data, bool replyExpected = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateStream(stream);
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            return SendBytesAsync(stream, function, (data ?? new SecsValue()).ToSourceBytes(StringEncoding), replyExpected, cancellationToken);
        }
        /// <summary>인코딩된 본문을 복사하여 전송합니다. 응답을 기다리지 않습니다.</summary>
        public Task SendBytesAsync(byte stream, byte function, byte[] data, bool replyExpected = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateStream(stream);
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            var message = new SecsMessage(DeviceID, stream, function, NextID(), data, replyExpected);
            return SendMessageAsync(message, cancellationToken);
        }
        /// <summary>수신한 primary의 장치 ID와 System Bytes를 보존하여 응답합니다.</summary>
        public Task ReplyAsync(SecsMessage request, SecsValue data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.SessionType != 0 || !request.W)
                throw new ArgumentException("Reply requires a data primary with W=1.", nameof(request));
            ValidatePrimary(request.StreamNo, request.FunctionNo);
            EnsureOpen();
            cancellationToken.ThrowIfCancellationRequested();
            return SendMessageAsync(new SecsMessage(request.DeviceID, request.StreamNo, (byte)(request.FunctionNo + 1),
                request.MessageID, (data ?? new SecsValue()).ToSourceBytes(StringEncoding)), cancellationToken);
        }
        /// <summary>Select.req와 Select.rsp를 교환합니다. 거절은 예외입니다.</summary>
        public async Task SelectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            SecsMessage result = await ControlAsync(1, 2, cancellationToken).ConfigureAwait(false);
            if (result.FunctionNo != 0 && result.FunctionNo != 1)
                throw new SecsProtocolException("HSMS Select was rejected.", result.FunctionNo);
            lock (sync)
            {
                EnsureOpenLocked();
                selected = true;
            }
        }
        /// <summary>Linktest.req와 Linktest.rsp를 교환합니다.</summary>
        public async Task LinkTestAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            SecsMessage result = await ControlAsync(5, 6, cancellationToken).ConfigureAwait(false);
            if (result.FunctionNo != 0)
                throw new SecsProtocolException("Invalid Linktest response.", result.FunctionNo);
        }
        private Task<SecsMessage> ControlAsync(byte request, byte response, CancellationToken token)
        {
            EnsureOpen();
            token.ThrowIfCancellationRequested();
            return ExchangeAsync(new SecsMessage(65535, 0, 0, NextID(), sessionType: request), response, 0, token);
        }
        private async Task<SecsMessage> ExchangeAsync(SecsMessage request, byte responseType, byte responseFunction, CancellationToken token)
        {
            byte[] frame = EncodeBounded(request);
            var waiter = new Pending(request, responseType, responseFunction);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(timeout);
                lock (sync)
                {
                    EnsureOpenLocked();
                    if (pending.Count >= 1024)
                        throw new InvalidOperationException("Too many pending HSMS requests.");
                    pending.Add(request.MessageID, waiter);
                }
                try
                {
                    using (deadline.Token.Register(() => waiter.Result.TrySetCanceled()))
                    {
                        await connection.SendAsync(frame, deadline.Token, EnsureOpen).ConfigureAwait(false);
                        SecsMessage response = await waiter.Result.Task.ConfigureAwait(false);
                        deadline.Token.ThrowIfCancellationRequested();
                        return response;
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new TimeoutException("HSMS request exceeded its total deadline.");
                }
                finally
                {
                    lock (sync)
                        pending.Remove(request.MessageID);
                    // Observe a receive failure even when send failed before awaiting the waiter.
                    if (waiter.Result.Task.IsFaulted)
                    {
                        var observed = waiter.Result.Task.Exception;
                    }
                }
            }
        }
        private async Task SendMessageAsync(SecsMessage message, CancellationToken token)
        {
            EnsureOpen();
            await SendFrameAsync(message, token, EnsureOpen).ConfigureAwait(false);
        }
        private async Task SendFrameAsync(SecsMessage message, CancellationToken token, Action validateSession)
        {
            byte[] frame = EncodeBounded(message);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(timeout);
                try { await connection.SendAsync(frame, deadline.Token, validateSession).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    throw new TimeoutException("HSMS send exceeded its total deadline.");
                }
            }
        }
        private byte[] EncodeBounded(SecsMessage message)
        {
            if (message.Body.Length > maximum - 14)
                throw new ArgumentOutOfRangeException(nameof(message), "HSMS frame exceeds its configured maximum.");
            return HsmsCodec.Encode(message);
        }
        private async Task ReceiveLoopAsync()
        {
            try
            {
                var boundary = HsmsCodec.CreateFrameBoundary(maximum);
                while (!IsClosed())
                {
                    SecsMessage message = HsmsCodec.Decode(await connection.ReceiveAsync(boundary).ConfigureAwait(false), maximum);
                    if (message.PresentationType != 0)
                        throw new InvalidDataException("Only SECS-II PType=0 is supported.");
                    if (message.SessionType != 0)
                    {
                        await HandleControlAsync(message).ConfigureAwait(false);
                        continue;
                    }
                    Pending waiter;
                    lock (sync)
                        pending.TryGetValue(message.MessageID, out waiter);
                    if (waiter != null && waiter.ResponseType == 0 && !message.W &&
                        message.DeviceID == waiter.Request.DeviceID && message.StreamNo == waiter.Request.StreamNo &&
                        (message.FunctionNo == waiter.ResponseFunction || message.FunctionNo == 0))
                    {
                        if (message.FunctionNo == 0)
                            waiter.Result.TrySetException(new SecsProtocolException("SECS transaction aborted by peer.", 0));
                        else
                            waiter.Result.TrySetResult(message);
                        continue;
                    }
                    if (IsOpen && automaticGemReplies && message.W && message.FunctionNo % 2 == 1)
                    {
                        SecsValue reply = AutomaticReply(message);
                        if (reply != null)
                        {
                            await ReplyAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }
                    }
                    EnqueueNotification(message);
                }
            }
            catch (Exception error)
            {
                Close(error);
            }
            finally { Close(null); }
        }
        private async Task HandleControlAsync(SecsMessage message)
        {
            if (message.DeviceID != 65535 || message.W || message.StreamNo != 0 || message.Body.Length != 0)
                throw new InvalidDataException("Malformed HSMS control message.");
            Pending waiter;
            lock (sync)
                pending.TryGetValue(message.MessageID, out waiter);
            if (waiter != null && waiter.ResponseType == message.SessionType)
            {
                waiter.Result.TrySetResult(message);
                return;
            }
            switch (message.SessionType)
            {
                case 1:
                    byte status;
                    lock (sync)
                    {
                        status = selected ? (byte)1 : (byte)0;
                        selected = true;
                    }
                    await SendMessageAsync(new SecsMessage(65535, 0, status, message.MessageID, sessionType: 2), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 3:
                    lock (sync)
                        selected = false;
                    await SendMessageAsync(new SecsMessage(65535, 0, 0, message.MessageID, sessionType: 4), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 5:
                    await SendMessageAsync(new SecsMessage(65535, 0, 0, message.MessageID, sessionType: 6), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 9:
                    Close(null);
                    break;
                case 2:
                case 4:
                case 6:
                case 7:
                    EnqueueNotification(message);
                    break;
                default:
                    throw new InvalidDataException("Unsupported HSMS SType.");
            }
        }
        private static SecsValue AutomaticReply(SecsMessage message)
        {
            if (message.StreamNo == 1 && message.FunctionNo == 1)
                return SecsValue.EmptyListValue();
            if (message.StreamNo == 1 && message.FunctionNo == 13)
                return new SecsValue(new object[] { new byte[] { 0 }, SecsValue.EmptyListValue() });
            if (message.StreamNo == 2 && message.FunctionNo == 17)
                return new SecsValue(DateTime.Now.ToString("yyyyMMddHHmmssff", CultureInfo.InvariantCulture));
            return null;
        }
        private void EnqueueNotification(SecsMessage message)
        {
            lock (sync)
            {
                if (closed || closing)
                    return;
                if (notifications.Count >= 1024)
                    throw new InvalidDataException("HSMS message callback queue is full.");
                notifications.Enqueue(message);
                if (!dispatching)
                {
                    dispatching = true;
                    Task.Run(DispatchNotifications);
                }
            }
        }
        private void DispatchNotifications()
        {
            while (true)
            {
                SecsMessage message;
                lock (sync)
                {
                    if (closed || closing || notifications.Count == 0)
                    {
                        dispatching = false;
                        return;
                    }
                    message = notifications.Dequeue();
                }
                try { MessageReceived?.Invoke(this, message); }
                catch (Exception error)
                {
                    lock (sync)
                        lastHandlerError = error;
                }
            }
        }
        private uint NextID()
        {
            lock (sync)
            {
                EnsureOpenLocked();
                do { nextID++; } while (nextID == 0 || pending.ContainsKey(nextID));
                return nextID;
            }
        }
        private void EnsureOpen() { lock (sync) EnsureOpenLocked(); }
        private bool IsClosed() { lock (sync) return closed; }
        private void EnsureOpenLocked()
        {
            if (closed || closing)
                throw new ObjectDisposedException(nameof(HsmsSession));
        }
        private static void ValidateStream(byte stream)
        {
            if (stream > 127)
                throw new ArgumentOutOfRangeException(nameof(stream));
        }
        private static void ValidatePrimary(byte stream, byte function)
        {
            ValidateStream(stream);
            if (function == 0 || function == 255 || function % 2 == 0)
                throw new ArgumentOutOfRangeException(nameof(function), "A primary requires an odd function from 1 to 253.");
        }
        /// <summary>Separate.req를 보내고 연결을 닫습니다. 전송 실패·취소 시에도 연결을 정리합니다.</summary>
        public Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (sync)
            {
                if (closeTask != null)
                    return closeTask;
                if (closed)
                    return Completion;
                uint identity = NextID();
                closing = true;
                closeTask = Task.Run(() => FinishCloseAsync(identity, cancellationToken));
                return closeTask;
            }
        }
        private async Task FinishCloseAsync(uint identity, CancellationToken cancellationToken)
        {
            try
            {
                await SendFrameAsync(new SecsMessage(65535, 0, 0, identity, sessionType: 9), cancellationToken, () =>
                {
                    if (IsClosed())
                        throw new ObjectDisposedException(nameof(HsmsSession));
                }).ConfigureAwait(false);
            }
            finally
            {
                Close(null);
                await Completion.ConfigureAwait(false);
            }
        }
        /// <summary>Separate 전송 없이 즉시 연결을 닫고 진행 중인 요청을 실패시킵니다.</summary>
        public void Dispose() => Close(null);
        private void Close(Exception cause)
        {
            Pending[] requests;
            lock (sync)
            {
                if (closed)
                    return;
                closed = true;
                lastError = cause;
                requests = new List<Pending>(pending.Values).ToArray();
                pending.Clear();
                notifications.Clear();
            }
            connection.Dispose();
            foreach (var request in requests)
                request.Result.TrySetException(cause ?? new IOException("HSMS session closed."));
        }
        private sealed class Pending
        {
            internal readonly SecsMessage Request;
            internal readonly byte ResponseType, ResponseFunction;
            internal readonly TaskCompletionSource<SecsMessage> Result =
                new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Pending(SecsMessage request, byte responseType, byte responseFunction)
            {
                Request = request; ResponseType = responseType; ResponseFunction = responseFunction;
            }
        }
    }
}
