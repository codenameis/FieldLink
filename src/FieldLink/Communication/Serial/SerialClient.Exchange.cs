using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Serial
{
    public sealed partial class SerialClient
    {
        /// <inheritdoc />
        public Task SendAsync(byte[] message, CancellationToken cancellationToken = default(CancellationToken)) => SendAsync(message, timeout, cancellationToken);
        /// <inheritdoc />
        public Task SendAsync(byte[] message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return ExecuteTransactionAsync(async tx => { await tx.SendAsync(message).ConfigureAwait(false); return true; }, timeout, cancellationToken);
        }
        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken)) => ExchangeAsync(request, boundary, timeout, classifyResponse, cancellationToken);
        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, TimeSpan timeout, Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken)) => ExchangeAsync(request, boundary, timeout, classifyResponse, cancellationToken);
        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));
            return ExecuteTransactionAsync(tx => tx.ExchangeAsync(request, boundary, classifyResponse), timeout, cancellationToken);
        }
        /// <inheritdoc />
        public Task<T> ExecuteTransactionAsync<T>(Func<ISerialTransaction, Task<T>> action,
            CancellationToken cancellationToken = default(CancellationToken)) => ExecuteTransactionAsync(action, timeout, cancellationToken);
        /// <inheritdoc />
        public Task<T> ExecuteTransactionAsync<T>(Func<ISerialTransaction, Task<T>> action, TimeSpan timeout,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            ClientOperation.ValidateTimeout(timeout); cancellationToken.ThrowIfCancellationRequested();
            if (inTransaction.Value)
                throw new InvalidOperationException("트랜잭션 안에서는 전달받은 교환 객체를 사용하세요.");
            PortSession session;
            lock (sync)
            {
                ThrowIfDisposed();
                if (current == null || State != ClientState.Open)
                    throw new InvalidOperationException("포트를 먼저 열어야 합니다.");
                session = current;
            }
            return ExecuteCoreAsync(action, session, timeout, cancellationToken);
        }
        private async Task<T> ExecuteCoreAsync<T>(Func<ISerialTransaction, Task<T>> action, PortSession session, TimeSpan timeout, CancellationToken caller)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller, session.Closed.Token))
            {
                deadline.CancelAfter(timeout);
                var transaction = new SerialTransaction(session.Port, session.Buffer, boundary, session.Generation, deadline.Token, timeout, settings.PortName, session.Timing);
                bool entered = false;
                try
                {
                    await gate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    entered = true;
                    deadline.Token.ThrowIfCancellationRequested();
                    lock (sync)
                        if (!ReferenceEquals(current, session) || State != ClientState.Open)
                            throw new OperationCanceledException(session.Closed.Token);
                    using (var interrupt = deadline.Token.Register(() => ReleasePort(session)))
                    {
                        inTransaction.Value = true;
                        T result = await transaction.ExecuteAsync(action).ConfigureAwait(false);
                        interrupt.Dispose();
                        deadline.Token.ThrowIfCancellationRequested();
                        lock (sync)
                            if (!ReferenceEquals(current, session))
                                throw new OperationCanceledException(session.Closed.Token);
                        return result;
                    }
                }
                catch (Exception error)
                {
                    Exception translated = Translate(error, caller, session, deadline.Token, entered ? transaction.Stage : CommunicationStage.Waiting, transaction.BytesSent, transaction.BytesReceived);
                    // 대기 중 실패는 이미 사용 중인 포트를 닫지 않는다.
                    if (entered)
                        Invalidate(session, translated);
                    throw translated;
                }
                finally
                {
                    inTransaction.Value = false;
                    transaction.End();
                    if (entered)
                        gate.Release();
                }
            }
        }
    }
}
