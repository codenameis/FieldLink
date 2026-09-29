using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;

namespace FieldLink.Communication.Tcp
{
    // One receive pump and serialized sends may run concurrently. Owns the accepted/connected socket.
    internal sealed class TcpDuplexConnection : IDisposable
    {
        private readonly System.Net.Sockets.TcpClient socket;
        private readonly NetworkStream stream;
        private readonly FrameAccumulator incoming;
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private int disposed;
        internal TcpDuplexConnection(System.Net.Sockets.TcpClient socket, int maximum)
        {
            this.socket = socket;
            stream = socket.GetStream();
            RemoteEndPoint = socket.Client.RemoteEndPoint;
            incoming = new FrameAccumulator(maximum);
        }
        internal EndPoint RemoteEndPoint { get; }
        internal async Task<byte[]> ReceiveAsync(IFrameBoundary boundary)
        {
            long received = 0;
            try
            {
                while (true)
                {
                    byte[] frame = incoming.TryTake(boundary);
                    if (frame != null)
                        return frame;
                    var free = incoming.FreeSpace();
                    int count = await stream.ReadAsync(free.Array, free.Offset, free.Count).ConfigureAwait(false);
                    if (count == 0)
                        throw new EndOfStreamException("The TCP peer closed the connection.");
                    received += count;
                    incoming.Advance(count);
                }
            }
            catch (Exception error)
            {
                Dispose();
                throw Failure(error, CommunicationStage.Receiving, 0, received);
            }
        }
        internal async Task SendAsync(byte[] message, CancellationToken cancellationToken, Action validateSession)
        {
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Check the owning protocol session after waiting, before starting socket I/O.
                validateSession();
                await WriteAsync(message, cancellationToken).ConfigureAwait(false);
            }
            finally { sendGate.Release(); }
        }
        private async Task WriteAsync(byte[] message, CancellationToken cancellationToken)
        {
            Task write = null;
            try
            {
                write = stream.WriteAsync(message, 0, message.Length, cancellationToken);
                using (cancellationToken.Register(() =>
                {
                    // A delayed continuation must not close a socket after the write already completed.
                    if (!write.IsCompleted)
                        Dispose();
                }))
                {
                    await write.ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception error)
            {
                if (write != null && (write.IsFaulted || write.IsCanceled || !write.IsCompleted))
                    Dispose();
                else if (!cancellationToken.IsCancellationRequested)
                    Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw Failure(error, CommunicationStage.Sending, 0, 0);
            }
        }
        private CommunicationException Failure(Exception cause, CommunicationStage stage, long sent, long received)
        {
            var socketError = cause as SocketException ?? cause.InnerException as SocketException;
            CommunicationFailure failure = cause is InvalidDataException ? CommunicationFailure.InvalidFrame :
                socketError != null ? CommunicationFailure.SocketError : CommunicationFailure.ConnectionClosed;
            return new CommunicationException(failure, stage, RemoteEndPoint.ToString(), sent, received, cause);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                socket.Dispose();
        }
    }
}
