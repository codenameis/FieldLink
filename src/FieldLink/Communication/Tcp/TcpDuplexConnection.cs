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
    // 수신은 한 작업이 담당하고, 송신은 sendGate로 한 번에 하나씩 실행합니다.
    // 수신과 송신은 동시에 진행할 수 있습니다. 소켓의 최종 해제는 이 객체가 담당합니다.
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
            long totalReceivedBytes = 0;
            try
            {
                while (true)
                {
                    byte[] frame = incoming.TryTake(boundary);
                    if (frame != null)
                        return frame;

                    // TCP의 한 번 읽기는 메시지 하나와 같지 않습니다. 완성될 때까지 모읍니다.
                    ArraySegment<byte> freeBufferSpace = incoming.FreeSpace();
                    int receivedBytes = await stream.ReadAsync(
                        freeBufferSpace.Array, freeBufferSpace.Offset, freeBufferSpace.Count).ConfigureAwait(false);
                    if (receivedBytes == 0)
                        throw new EndOfStreamException("The TCP peer closed the connection.");

                    totalReceivedBytes += receivedBytes;
                    incoming.Advance(receivedBytes);
                }
            }
            catch (Exception error)
            {
                // 여기서 바로 해제하면 진행 중인 종료 메시지 송신이 실패할 수 있습니다.
                // 세션이 송신 완료와 소켓 해제의 순서를 결정하도록 오류만 전달합니다.
                throw CreateFailureException(error, CommunicationStage.Receiving, 0, totalReceivedBytes);
            }
        }

        internal async Task SendAsync(byte[] message, CancellationToken cancellationToken, Action validateSession)
        {
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 송신 차례를 기다리는 동안 세션이 닫혔을 수 있으므로 다시 확인합니다.
                validateSession();
                await WriteAsync(message, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                sendGate.Release();
            }
        }

        private async Task WriteAsync(byte[] message, CancellationToken cancellationToken)
        {
            Task writeTask = null;
            try
            {
                writeTask = stream.WriteAsync(message, 0, message.Length, cancellationToken);
                using (cancellationToken.Register(() =>
                {
                    // 취소 통지가 늦게 도착해도 이미 완료된 송신의 소켓은 닫지 않습니다.
                    if (!writeTask.IsCompleted)
                        Dispose();
                }))
                {
                    await writeTask.ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception error)
            {
                if (writeTask != null && (writeTask.IsFaulted || writeTask.IsCanceled || !writeTask.IsCompleted))
                {
                    Dispose();
                }
                else if (!cancellationToken.IsCancellationRequested)
                {
                    Dispose();
                }

                // 호출자 취소는 통신 오류로 바꾸지 않고 취소 예외로 전달합니다.
                cancellationToken.ThrowIfCancellationRequested();
                throw CreateFailureException(error, CommunicationStage.Sending, 0, 0);
            }
        }
        private CommunicationException CreateFailureException(Exception cause, CommunicationStage stage, long sent, long received)
        {
            SocketException socketError = cause as SocketException ?? cause.InnerException as SocketException;
            CommunicationFailure failure;
            if (cause is InvalidDataException)
            {
                failure = CommunicationFailure.InvalidFrame;
            }
            else if (socketError != null)
            {
                failure = CommunicationFailure.SocketError;
            }
            else
            {
                failure = CommunicationFailure.ConnectionClosed;
            }

            return new CommunicationException(failure, stage, RemoteEndPoint.ToString(), sent, received, cause);
        }

        public void Dispose()
        {
            // 여러 종료 경로가 겹쳐도 소켓을 해제하는 작업은 한 번만 수행합니다.
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                socket.Dispose();
        }
    }
}
