using FieldLink.Communication.Internal;
using FieldLink.Communication.Diagnostics;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tcp
{
    // 연결별 버퍼다. 첫 프레임을 꺼낸 뒤 남은 바이트는 다음 수신에서 사용한다.
    internal sealed class TcpFrameBuffer
    {
        private readonly int maximumFrameLength;
        private byte[] buffer;
        private int bufferedByteCount;

        internal bool HasBufferedData => bufferedByteCount != 0;

        internal TcpFrameBuffer(int maximumFrameLength)
        {
            this.maximumFrameLength = maximumFrameLength;
            buffer = new byte[Math.Min(4096, maximumFrameLength)];
        }

        internal async Task<byte[]> ReadAsync(Socket socket, IFrameBoundary boundary, ClientOperation operation)
        {
            while (true)
            {
                operation.Token.ThrowIfCancellationRequested();
                operation.Stage = CommunicationStage.Validating;
                int? frameLength = boundary.GetFrameLength(new ArraySegment<byte>(buffer, 0, bufferedByteCount));
                if (frameLength.HasValue)
                {
                    if (frameLength <= 0)
                        throw operation.Failure(CommunicationFailure.InvalidFrame, "Frame length must be positive.");
                    if (frameLength > maximumFrameLength)
                        throw operation.Failure(CommunicationFailure.MessageTooLarge, $"Frame length {frameLength} exceeds {maximumFrameLength} bytes.");
                    if (bufferedByteCount >= frameLength.Value)
                        return TakeFrame(frameLength.Value);
                }

                if (bufferedByteCount == maximumFrameLength)
                    throw operation.Failure(CommunicationFailure.MessageTooLarge, "No complete frame within the configured size limit.");

                EnsureFreeSpace();
                operation.Stage = CommunicationStage.Receiving;
                int received = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, bufferedByteCount, buffer.Length - bufferedByteCount),
                    SocketFlags.None).ConfigureAwait(false);
                if (received == 0)
                    throw operation.Failure(CommunicationFailure.ConnectionClosed,
                        $"Peer closed the connection with {bufferedByteCount} buffered bytes.");

                bufferedByteCount += received;
                operation.BytesReceived += received;
            }
        }

        private void EnsureFreeSpace()
        {
            if (bufferedByteCount < buffer.Length)
                return;
            int capacity = (int)Math.Min((long)buffer.Length * 2, maximumFrameLength);
            Array.Resize(ref buffer, capacity);
        }

        private byte[] TakeFrame(int length)
        {
            byte[] frame = new byte[length];
            Buffer.BlockCopy(buffer, 0, frame, 0, length);
            bufferedByteCount -= length;
            Buffer.BlockCopy(buffer, length, buffer, 0, bufferedByteCount);
            return frame;
        }
    }
}
