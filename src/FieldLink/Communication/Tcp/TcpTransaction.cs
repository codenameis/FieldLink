using System;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tcp
{
    /// <summary>
    /// 여러 TCP 교환을 하나의 독점 작업으로 묶습니다. 프로토콜 초기화 등 필요한 경우에만 사용하세요.
    /// ExecuteTransactionAsync 콜백 안에서 각 호출을 순서대로 await해야 합니다.
    /// </summary>
    internal sealed class TcpTransaction : ITcpTransaction
    {
        private readonly Socket socket;
        private readonly TcpExchange exchange;
        private readonly IFrameBoundary defaultBoundary;
        private int active = 1;
        private int busy;
        private ExceptionDispatchInfo failure;
        internal bool CloseRequested { get; private set; }
        public void CloseAfterCompletion()
        {
            Enter();
            try { CloseRequested = true; }
            finally { Volatile.Write(ref busy, 0); }
        }

        internal TcpTransaction(Socket socket, TcpExchange exchange, IFrameBoundary defaultBoundary,
            long connectionGeneration, CancellationToken cancellationToken)
        {
            this.socket = socket;
            this.exchange = exchange;
            this.defaultBoundary = defaultBoundary;
            ConnectionGeneration = connectionGeneration;
            CancellationToken = cancellationToken;
        }

        /// <inheritdoc />
        public long ConnectionGeneration { get; }
        /// <inheritdoc />
        public CancellationToken CancellationToken { get; }

        /// <summary>메시지를 전송합니다. 장치 응답을 기다리지 않습니다.</summary>
        public Task SendAsync(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return RunStepAsync(async () =>
            {
                await exchange.SendAsync(data).ConfigureAwait(false);
                return true;
            });
        }

        /// <summary>메시지 하나를 수신합니다. boundary를 생략하면 연결의 기본 규칙을 사용합니다.</summary>
        public Task<byte[]> ReceiveAsync(IFrameBoundary boundary = null)
        {
            return RunStepAsync(() => exchange.ReceiveAsync(boundary ?? defaultBoundary));
        }

        /// <summary>요청을 보내고 응답 하나를 받습니다. 전체 트랜잭션의 마감 시간을 공유합니다.</summary>
        public Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary = null,
            Func<byte[], ResponseDisposition> classifyResponse = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return RunStepAsync(() => exchange.ExchangeAsync(request, boundary ?? defaultBoundary, classifyResponse));
        }

        private async Task<T> RunStepAsync<T>(Func<Task<T>> action)
        {
            Enter();
            try { return await action().ConfigureAwait(false); }
            catch (Exception cause)
            {
                failure = ExceptionDispatchInfo.Capture(cause);
                End();
                socket.Dispose();
                throw;
            }
            finally { Volatile.Write(ref busy, 0); }
        }

        private void Enter()
        {
            if (Volatile.Read(ref active) == 0)
                throw new InvalidOperationException("The TCP transaction has ended.");
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
                throw new InvalidOperationException("Await each TCP transaction operation before starting another.");
            if (Volatile.Read(ref active) == 0)
            {
                Volatile.Write(ref busy, 0);
                throw new InvalidOperationException("The TCP transaction has ended.");
            }
        }

        internal async Task<T> ExecuteAsync<T>(Func<ITcpTransaction, Task<T>> action)
        {
            try
            {
                CancellationToken.ThrowIfCancellationRequested();
                Task<T> work = action(this) ?? throw new InvalidOperationException("Transaction callback returned a null task.");
                T result = await AwaitCallbackAsync(work, CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested();
                End();
                failure?.Throw();
                if (Volatile.Read(ref busy) != 0)
                    throw new InvalidOperationException("Transaction callback ended before its socket operation completed.");
                return result;
            }
            finally { End(); }
        }

        private void End() => Interlocked.Exchange(ref active, 0);

        private static async Task<T> AwaitCallbackAsync<T>(Task<T> work, CancellationToken token)
        {
            var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancellation.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, cancellation.Task).ConfigureAwait(false) != work)
                {
                    // 취소된 콜백이 나중에 끝나더라도 예외를 관찰하고 전송은 재개하지 않는다.
                    _ = work.ContinueWith(failed => { _ = failed.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    token.ThrowIfCancellationRequested();
                }
                return await work.ConfigureAwait(false);
            }
        }
    }
}
