using FieldLink.Communication.Internal;
using FieldLink.Communication.Diagnostics;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tcp
{
    // 이미 독점권을 얻은 작업의 송수신만 담당한다. 수명·잠금·사용자 콜백은 소유하지 않는다.
    internal sealed class TcpExchange
    {
        private readonly Socket socket;
        private readonly TcpFrameBuffer reader;
        private readonly ClientOperation operation;

        internal TcpExchange(Socket socket, TcpFrameBuffer reader, ClientOperation operation)
        {
            this.socket = socket;
            this.reader = reader;
            this.operation = operation;
        }

        internal async Task SendAsync(byte[] data)
        {
            operation.Stage = CommunicationStage.Sending;
            int offset = 0;
            while (offset < data.Length)
            {
                operation.Token.ThrowIfCancellationRequested();
                int sent = await socket.SendAsync(new ArraySegment<byte>(data, offset, data.Length - offset),
                    SocketFlags.None).ConfigureAwait(false);
                if (sent == 0)
                    throw operation.Failure(CommunicationFailure.ConnectionClosed, "Socket accepted zero bytes during send.");
                offset += sent;
                operation.BytesSent += sent;
            }
        }

        internal Task<byte[]> ReceiveAsync(IFrameBoundary boundary) => reader.ReadAsync(socket, boundary, operation);

        internal async Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary,
            Func<byte[], ResponseDisposition> classifyResponse)
        {
            await SendAsync(request).ConfigureAwait(false);
            while (true)
            {
                byte[] response = await ReceiveAsync(boundary).ConfigureAwait(false);
                operation.Stage = CommunicationStage.Validating;
                ResponseDisposition disposition = classifyResponse?.Invoke(response) ?? ResponseDisposition.Accept;
                if (disposition == ResponseDisposition.Accept)
                    return response;
                if (disposition != ResponseDisposition.Ignore)
                    throw operation.Failure(CommunicationFailure.ResponseRejected, "Response classifier rejected the frame.");
            }
        }
    }
}
