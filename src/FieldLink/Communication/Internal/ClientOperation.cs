using FieldLink.Communication.Diagnostics;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace FieldLink.Communication.Internal
{
    // 대기열 진입부터 마지막 수신까지 하나의 취소 토큰과 마감 시간을 사용한다.
    internal sealed class ClientOperation : IDisposable
    {
        private readonly CancellationToken callerCancellation;
        private readonly CancellationToken lifetimeCancellation;
        private readonly CancellationTokenSource deadline;
        private readonly string remoteAddress;
        private readonly CancellationToken token;
        private ClientSocket owner;
        private CancellationTokenRegistration interrupt;

        internal ClientOperation(TimeSpan timeout, CancellationToken callerCancellation,
            CancellationToken lifetimeCancellation, CancellationToken connectionCancellation,
            IPEndPoint remoteEndPoint)
        {
            ValidateTimeout(timeout);

            this.callerCancellation = callerCancellation;
            this.lifetimeCancellation = lifetimeCancellation;
            ConnectionCancellation = connectionCancellation;
            remoteAddress = remoteEndPoint.ToString();
            deadline = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellation, lifetimeCancellation, connectionCancellation);
            token = deadline.Token;
            deadline.CancelAfter(timeout);
        }

        internal CancellationToken Token => token;
        internal CancellationToken ConnectionCancellation { get; }
        internal Socket Socket { get; private set; }

        internal void OwnGate(ClientSocket owner) => this.owner = owner;

        internal void AttachSocket(Socket socket)
        {
            Socket = socket;
            interrupt = Token.Register(socket.Dispose);
        }

        internal void Complete()
        {
            interrupt.Dispose();
            Token.ThrowIfCancellationRequested();
            owner.VerifyCurrentSocket(Socket, this);
        }

        internal void CompleteOpen(IDisposable monitor = null)
        {
            try
            {
                Complete();
                owner.CompleteOpen(this, monitor);
            }
            catch
            {
                monitor?.Dispose();
                throw;
            }
        }

        internal void Fail(Exception cause)
        {
            interrupt.Dispose();
            Exception translated = Translate(cause);
            owner?.Invalidate(Socket, translated);
            if (!ReferenceEquals(translated, cause))
                throw translated;
        }

        internal static void ValidateTimeout(TimeSpan timeout)
        {
            if (timeout < TimeSpan.FromMilliseconds(1) || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Use a timeout between 1 and Int32.MaxValue milliseconds.");
        }
        internal CommunicationStage Stage { get; set; } = CommunicationStage.Waiting;
        internal long BytesSent { get; set; }
        internal long BytesReceived { get; set; }

        internal CommunicationException Failure(CommunicationFailure failure, string message)
        {
            return new CommunicationException(failure, Stage, remoteAddress,
                BytesSent, BytesReceived, new IOException(message));
        }

        internal Exception Translate(Exception cause)
        {
            CommunicationFailure failure;
            if (lifetimeCancellation.IsCancellationRequested)
                failure = CommunicationFailure.Disposed;
            else if (callerCancellation.IsCancellationRequested)
                failure = CommunicationFailure.Cancelled;
            else if (ConnectionCancellation.IsCancellationRequested)
                failure = CommunicationFailure.ConnectionClosed;
            else if (Token.IsCancellationRequested)
                failure = CommunicationFailure.Timeout;
            else if (cause is CommunicationException)
                return cause;
            else if (cause is SocketException socketError)
                failure = socketError.SocketErrorCode == SocketError.MessageSize
                    ? CommunicationFailure.MessageTooLarge : CommunicationFailure.SocketError;
            else if (cause is ObjectDisposedException)
                failure = CommunicationFailure.ConnectionClosed;
            else if (cause is InvalidDataException)
                failure = CommunicationFailure.InvalidFrame;
            else return cause; // 호출자 코드의 버그까지 통신 오류로 숨기지 않는다.

            return new CommunicationException(failure, Stage, remoteAddress, BytesSent, BytesReceived, cause);
        }

        public void Dispose()
        {
            interrupt.Dispose();
            deadline.Dispose();
            ClientSocket completedOwner = owner;
            owner = null;
            completedOwner?.ReleaseRequest();
        }
    }
}
