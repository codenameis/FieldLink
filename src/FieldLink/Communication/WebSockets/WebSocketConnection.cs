using FieldLink.Communication.Diagnostics;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.WebSockets
{
    /// <summary>WebSocket 연결 하나의 송수신과 수명을 관리합니다.</summary>
    public sealed class WebSocketConnection : IDisposable
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly System.Net.WebSockets.WebSocket socket;
        private readonly string remoteAddress;
        private readonly int maximumMessageBytes;
        private readonly TimeSpan sendTimeout;
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim receiveGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim closeGate = new SemaphoreSlim(1, 1);
        private readonly TaskCompletionSource<bool> closeAcknowledged =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int disposed;

        internal WebSocketConnection(System.Net.WebSockets.WebSocket socket, string remoteAddress,
            int maximumMessageBytes, TimeSpan sendTimeout)
        {
            this.socket = socket ?? throw new ArgumentNullException(nameof(socket));
            this.remoteAddress = remoteAddress ?? string.Empty;
            if (maximumMessageBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumMessageBytes));
            if (sendTimeout.TotalMilliseconds < 1 || sendTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(sendTimeout));
            this.maximumMessageBytes = maximumMessageBytes;
            this.sendTimeout = sendTimeout;
        }

        /// <summary>로컬 WebSocket 상태입니다. 상대의 즉각적인 생존을 보장하지 않습니다.</summary>
        public bool IsOpen => Volatile.Read(ref disposed) == 0 && socket.State == WebSocketState.Open;
        /// <summary>연결 대상 또는 접속한 상대의 주소입니다.</summary>
        public string RemoteAddress => remoteAddress;

        /// <summary>UTF-8 텍스트를 보냅니다. 동시 송신은 순서대로 실행합니다.</summary>
        public Task SendTextAsync(string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return SendAsync(StrictUtf8.GetBytes(text), WebSocketMessageType.Text, cancellationToken);
        }

        /// <summary>바이트를 복사하지 않고 보냅니다. 작업 완료 전까지 배열을 변경하거나 재사용하지 마세요.</summary>
        public Task SendBinaryAsync(byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            return SendAsync(payload, WebSocketMessageType.Binary, cancellationToken);
        }

        private async Task SendAsync(byte[] payload, WebSocketMessageType type, CancellationToken cancellationToken)
        {
            if (payload.Length > maximumMessageBytes)
                throw new ArgumentOutOfRangeException(nameof(payload), "WebSocket 메시지 크기 제한을 초과했습니다.");
            EnsureOpen();
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 대기 중 Close가 시작된 호출은 거부만 한다. 정상 종료의 소켓을 폐기하지 않는다.
                EnsureOpen();
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    deadline.CancelAfter(sendTimeout);
                    try { await socket.SendAsync(new ArraySegment<byte>(payload), type, true, deadline.Token).ConfigureAwait(false); }
                    catch (Exception error)
                    {
                        Dispose();
                        if (error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                            throw new CommunicationException(CommunicationFailure.Timeout, CommunicationStage.Sending,
                                remoteAddress, 0, 0, error);
                        throw;
                    }
                }
            }
            finally { sendGate.Release(); }
        }

        /// <summary>조각을 합쳐 메시지 하나를 받습니다. 상대가 닫으면 null을 반환합니다.</summary>
        public async Task<WebSocketMessage> ReceiveAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureOpen();
            await receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 진입 상태 검사와 실제 수신 실패의 정리 책임을 구분한다.
                EnsureOpen();
                return await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { receiveGate.Release(); }
        }

        private async Task<WebSocketMessage> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[8192];
                using (var payload = new MemoryStream())
                {
                    WebSocketMessageType? type = null;
                    while (true)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            if (await AcknowledgeCloseAsync().ConfigureAwait(false))
                                closeAcknowledged.TrySetResult(true);
                            Dispose();
                            return null;
                        }
                        if (!type.HasValue)
                            type = result.MessageType;
                        if (type != result.MessageType)
                            throw new InvalidDataException("메시지 조각의 형식이 변경되었습니다.");
                        if (result.Count > maximumMessageBytes - payload.Length)
                            throw new CommunicationException(CommunicationFailure.MessageTooLarge, CommunicationStage.Receiving,
                                remoteAddress, 0, payload.Length, new InvalidDataException("WebSocket 메시지 크기 제한을 초과했습니다."));
                        payload.Write(buffer, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            try { return new WebSocketMessage(type.Value, payload.ToArray()); }
                            catch (DecoderFallbackException error)
                            {
                                throw new CommunicationException(CommunicationFailure.InvalidFrame, CommunicationStage.Validating,
                                    remoteAddress, 0, payload.Length, error);
                            }
                        }
                    }
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private async Task<bool> AcknowledgeCloseAsync()
        {
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                try
                {
                    await sendGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    try
                    {
                        if (socket.State == WebSocketState.CloseReceived)
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, deadline.Token).ConfigureAwait(false);
                        return socket.State == WebSocketState.Closed;
                    }
                    finally { sendGate.Release(); }
                }
                catch (Exception error) when (error is WebSocketException || error is OperationCanceledException || error is ObjectDisposedException)
                { return false; }
            }
        }

        /// <summary>상대의 Close 응답까지 기다립니다. 2초 안에 완료되지 않으면 연결을 중단합니다.</summary>
        public Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            CloseCoreAsync(cancellationToken, true);

        // 처리 함수 종료 후에도 동일한 수신 잠금을 사용해 Close 응답을 확인합니다.
        internal Task CloseAfterHandlerAsync() => CloseCoreAsync(CancellationToken.None, false);

        private async Task CloseCoreAsync(CancellationToken cancellationToken, bool reportTimeout)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                bool closeAcquired = false;
                try
                {
                    await closeGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    closeAcquired = true;
                    if (Volatile.Read(ref disposed) != 0) return;
                    await sendGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    try
                    {
                        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                        {
                            Task closing = socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,
                                string.Empty, deadline.Token);
                            await AwaitBoundedAsync(closing, deadline.Token).ConfigureAwait(false);
                        }
                    }
                    finally { sendGate.Release(); }
                    if (socket.State == WebSocketState.Closed)
                    {
                        closeAcknowledged.TrySetResult(true);
                        return;
                    }
                    await receiveGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    try
                    {
                        if (closeAcknowledged.Task.IsCompleted)
                        {
                            if (await closeAcknowledged.Task.ConfigureAwait(false)) return;
                            throw new IOException("WebSocket Close 응답 전에 연결이 종료되었습니다.");
                        }
                        if (!await WaitForCloseAsync(deadline.Token).ConfigureAwait(false))
                            throw new IOException("WebSocket Close 응답을 받지 못했습니다.");
                        closeAcknowledged.TrySetResult(true);
                    }
                    finally { receiveGate.Release(); }
                }
                catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
                {
                    if (reportTimeout)
                        throw new CommunicationException(CommunicationFailure.Timeout, CommunicationStage.Receiving,
                            remoteAddress, 0, 0, error);
                }
                finally
                {
                    Dispose();
                    if (closeAcquired) closeGate.Release();
                }
            }
        }

        private static async Task AwaitBoundedAsync(Task operation, CancellationToken cancellationToken)
        {
            if (await Task.WhenAny(operation, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false) != operation)
            {
                ObserveFault(operation);
                cancellationToken.ThrowIfCancellationRequested();
            }
            await operation.ConfigureAwait(false);
        }

        private async Task<bool> WaitForCloseAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (socket.State == WebSocketState.CloseSent || socket.State == WebSocketState.CloseReceived ||
                socket.State == WebSocketState.Open)
            {
                Task<WebSocketReceiveResult> receive = socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cancellationToken);
                await AwaitBoundedAsync(receive, cancellationToken).ConfigureAwait(false);
                if ((await receive.ConfigureAwait(false)).MessageType == WebSocketMessageType.Close)
                    return true;
            }
            return socket.State == WebSocketState.Closed;
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(completed => { var ignored = completed.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        private void EnsureOpen()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(WebSocketConnection));
            if (socket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket 연결이 열려 있지 않습니다.");
        }

        /// <summary>활성 작업을 중단하고 연결을 해제합니다.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            socket.Abort();
            socket.Dispose();
            closeAcknowledged.TrySetResult(false);
        }
    }
}
