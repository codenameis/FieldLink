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
        private readonly TaskCompletionSource<bool> connectionReleased =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<uint, PendingRequest> pending = new Dictionary<uint, PendingRequest>();
        private readonly Queue<SecsMessage> notifications = new Queue<SecsMessage>();
        private readonly int maximum;
        private readonly TimeSpan timeout;
        private readonly bool automaticGemReplies;
        private uint nextMessageId;
        // closing이면 새 요청을 막고 Separate 송신을 마칩니다. closed이면 수신 처리도 끝냅니다.
        private bool closed;
        private bool closing;
        private bool dispatching;
        private bool selected;
        private Task closeTask;
        private Exception lastError;
        private Exception lastHandlerError;

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
        internal void Start()
        {
            Completion = Task.Run(ReceiveLoopAsync);
        }

        /// <summary>세션의 기본 데이터 장치 ID입니다.</summary>
        public ushort DeviceID { get; }
        /// <summary>문자열 인코딩입니다.</summary>
        public Encoding StringEncoding { get; }
        /// <summary>연결된 상대 주소입니다.</summary>
        public EndPoint RemoteEndPoint => connection.RemoteEndPoint;
        /// <summary>이 세션이 아직 닫히지 않았는지 표시합니다.</summary>
        public bool IsOpen
        {
            get
            {
                lock (sync)
                {
                    return !closed && !closing;
                }
            }
        }

        /// <summary>Select 절차를 완료한 상태입니다. 원본 호환 모드에서는 Select 없이 데이터 교환도 허용합니다.</summary>
        public bool IsSelected
        {
            get
            {
                lock (sync)
                {
                    return selected && !closed && !closing;
                }
            }
        }

        /// <summary>수신 루프의 종료입니다. 오류는 LastError에 보존됩니다.</summary>
        public Task Completion { get; private set; } = Task.CompletedTask;
        /// <summary>연결을 종료시킨 마지막 오류입니다.</summary>
        public Exception LastError
        {
            get
            {
                lock (sync)
                {
                    return lastError;
                }
            }
        }

        /// <summary>사용자 메시지 콜백에서 발생한 마지막 오류입니다.</summary>
        public Exception LastMessageHandlerError
        {
            get
            {
                lock (sync)
                {
                    return lastHandlerError;
                }
            }
        }

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
            return ExchangeAsync(new SecsMessage(DeviceID, stream, function, CreateMessageId(), data, true),
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
            var message = new SecsMessage(DeviceID, stream, function, CreateMessageId(), data, replyExpected);
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
            return ExchangeAsync(new SecsMessage(65535, 0, 0, CreateMessageId(), sessionType: request), response, 0, token);
        }
        private async Task<SecsMessage> ExchangeAsync(SecsMessage request, byte responseType, byte responseFunction, CancellationToken token)
        {
            byte[] frame = EncodeBounded(request);
            var pendingRequest = new PendingRequest(request, responseType, responseFunction);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(timeout);
                lock (sync)
                {
                    EnsureOpenLocked();
                    if (pending.Count >= 1024)
                        throw new InvalidOperationException("Too many pending HSMS requests.");
                    pending.Add(request.MessageID, pendingRequest);
                }
                try
                {
                    using (deadline.Token.Register(() => pendingRequest.Result.TrySetCanceled()))
                    {
                        await connection.SendAsync(frame, deadline.Token, EnsureOpen).ConfigureAwait(false);
                        SecsMessage response = await pendingRequest.Result.Task.ConfigureAwait(false);
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
                    {
                        pending.Remove(request.MessageID);
                    }

                    // 송신에서 먼저 실패해 응답 Task를 기다리지 못했어도 수신 오류를 확인합니다.
                    if (pendingRequest.Result.Task.IsFaulted)
                    {
                        var observed = pendingRequest.Result.Task.Exception;
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
                try
                {
                    await connection.SendAsync(frame, deadline.Token, validateSession).ConfigureAwait(false);
                }
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

                    // 제어 메시지는 Select, Linktest, Separate 같은 연결 절차를 처리합니다.
                    if (message.SessionType != 0)
                    {
                        await HandleControlAsync(message).ConfigureAwait(false);
                        continue;
                    }
                    PendingRequest pendingRequest;
                    lock (sync)
                    {
                        pending.TryGetValue(message.MessageID, out pendingRequest);
                    }

                    // 요청 ID와 응답 헤더가 모두 일치할 때만 기다리는 요청을 완료합니다.
                    if (pendingRequest != null && pendingRequest.ResponseType == 0 && !message.W &&
                        message.DeviceID == pendingRequest.Request.DeviceID && message.StreamNo == pendingRequest.Request.StreamNo &&
                        (message.FunctionNo == pendingRequest.ResponseFunction || message.FunctionNo == 0))
                    {
                        if (message.FunctionNo == 0)
                        {
                            pendingRequest.Result.TrySetException(new SecsProtocolException("SECS transaction aborted by peer.", 0));
                        }
                        else
                        {
                            pendingRequest.Result.TrySetResult(message);
                        }
                        continue;
                    }

                    // 자동 응답을 켠 세션은 지원하는 GEM 요청에 직접 답합니다.
                    if (IsOpen && automaticGemReplies && message.W && message.FunctionNo % 2 == 1)
                    {
                        SecsValue reply = AutomaticReply(message);
                        if (reply != null)
                        {
                            await ReplyAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }
                    }

                    // 요청의 응답이 아닌 메시지는 사용자 콜백 큐로 전달합니다.
                    EnqueueNotification(message);
                }
            }
            catch (Exception error)
            {
                Close(error);
            }
            finally
            {
                Close(null);
                // 소켓이 해제된 뒤 수신 완료를 알립니다. 그래야 서버가 정리 중인 세션을 놓치지 않습니다.
                await connectionReleased.Task.ConfigureAwait(false);
            }
        }
        private async Task HandleControlAsync(SecsMessage message)
        {
            if (message.DeviceID != 65535 || message.W || message.StreamNo != 0 || message.Body.Length != 0)
                throw new InvalidDataException("Malformed HSMS control message.");
            PendingRequest pendingRequest;
            lock (sync)
            {
                pending.TryGetValue(message.MessageID, out pendingRequest);
            }
            if (pendingRequest != null && pendingRequest.ResponseType == message.SessionType)
            {
                pendingRequest.Result.TrySetResult(message);
                return;
            }
            switch (message.SessionType)
            {
                case 1: // Select.req: 데이터 교환을 시작할 수 있는지 요청합니다.
                    byte status;
                    lock (sync)
                    {
                        status = selected ? (byte)1 : (byte)0;
                        selected = true;
                    }
                    await SendMessageAsync(new SecsMessage(65535, 0, status, message.MessageID, sessionType: 2), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 3: // Deselect.req: 데이터 교환 선택 상태를 해제합니다.
                    lock (sync)
                    {
                        selected = false;
                    }
                    await SendMessageAsync(new SecsMessage(65535, 0, 0, message.MessageID, sessionType: 4), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 5: // Linktest.req: 연결 확인 요청에 응답합니다.
                    await SendMessageAsync(new SecsMessage(65535, 0, 0, message.MessageID, sessionType: 6), default(CancellationToken)).ConfigureAwait(false);
                    break;
                case 9: // Separate.req: 상대가 연결 종료를 요청했습니다.
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
                // 사용자 코드가 다시 요청하거나 종료할 수 있으므로 잠금 밖에서 호출합니다.
                try
                {
                    MessageReceived?.Invoke(this, message);
                }
                catch (Exception error)
                {
                    lock (sync)
                    {
                        lastHandlerError = error;
                    }
                }
            }
        }
        private uint CreateMessageId()
        {
            lock (sync)
            {
                EnsureOpenLocked();
                do
                {
                    nextMessageId++;
                }
                while (nextMessageId == 0 || pending.ContainsKey(nextMessageId));

                return nextMessageId;
            }
        }
        private void EnsureOpen()
        {
            lock (sync)
            {
                EnsureOpenLocked();
            }
        }

        private bool IsClosed()
        {
            lock (sync)
            {
                return closed;
            }
        }

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

                uint identity = CreateMessageId();
                // 먼저 새 요청을 막습니다. 같은 세션의 중복 Close 호출은 같은 Task를 받습니다.
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
                // 완료 통지 전에 소켓을 해제해야 수신 종료와 서로 기다리는 상황을 피할 수 있습니다.
                Dispose();
                await Completion.ConfigureAwait(false);
            }
        }
        /// <summary>Separate 전송 없이 즉시 연결을 닫고 진행 중인 요청을 실패시킵니다.</summary>
        public void Dispose()
        {
            try
            {
                Close(null);
            }
            finally
            {
                // Dispose는 정상 종료 송신을 기다리지 않고 즉시 자원을 해제합니다.
                ReleaseConnection();
            }
        }

        private void ReleaseConnection()
        {
            try
            {
                connection.Dispose();
            }
            finally
            {
                connectionReleased.TrySetResult(true);
            }
        }

        private void Close(Exception cause)
        {
            PendingRequest[] requests;
            bool releaseConnectionImmediately;
            lock (sync)
            {
                if (closed)
                    return;
                closed = true;
                releaseConnectionImmediately = !closing;
                lastError = cause;
                requests = new List<PendingRequest>(pending.Values).ToArray();
                pending.Clear();
                notifications.Clear();
            }
            // 상대는 Separate를 받자마자 연결을 닫을 수 있습니다. 이때 로컬 송신 완료 처리는
            // 아직 남아 있을 수 있으므로 정상 Close가 마지막 송신과 자원 해제를 마치게 둡니다.
            if (releaseConnectionImmediately)
                ReleaseConnection();
            foreach (var request in requests)
                request.Result.TrySetException(cause ?? new IOException("HSMS session closed."));
        }
        private sealed class PendingRequest
        {
            internal readonly SecsMessage Request;
            internal readonly byte ResponseType;
            internal readonly byte ResponseFunction;
            internal readonly TaskCompletionSource<SecsMessage> Result =
                new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal PendingRequest(SecsMessage request, byte responseType, byte responseFunction)
            {
                Request = request;
                ResponseType = responseType;
                ResponseFunction = responseFunction;
            }
        }
    }
}
