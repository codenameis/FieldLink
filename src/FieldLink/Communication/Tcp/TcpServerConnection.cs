using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Tcp
{
    internal sealed class TcpServerConnection : ITcpServerConnection, IDisposable
    {
        private readonly System.Net.Sockets.TcpClient socket;
        private readonly NetworkStream stream;
        private readonly FrameAccumulator incoming;
        private readonly TimeSpan timeout;
        private readonly CancellationToken stop;
        private int busy, disposed;
        internal TcpServerConnection(System.Net.Sockets.TcpClient socket, int maximum, TimeSpan timeout, CancellationToken stop)
        {
            this.socket = socket; stream = socket.GetStream(); incoming = new FrameAccumulator(maximum);
            this.timeout = timeout; this.stop = stop; RemoteEndPoint = socket.Client.RemoteEndPoint;
        }
        public EndPoint RemoteEndPoint { get; }
        public async Task<byte[]> ReceiveAsync(IFrameBoundary boundary)
        {
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));
            if (boundary is IdleGapFrame)
                throw new ArgumentException("TCP 서버에는 길이 또는 종료 문자 경계를 지정하세요.", nameof(boundary));
            Enter();
            try
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop))
                {
                    deadline.CancelAfter(timeout);
                    using (deadline.Token.Register(Dispose))
                    {
                        while (true)
                        {
                            deadline.Token.ThrowIfCancellationRequested();
                            byte[] complete = incoming.TryTake(boundary);
                            if (complete != null)
                                return complete;
                            var free = incoming.FreeSpace();
                            int count = await stream.ReadAsync(free.Array, free.Offset, free.Count, deadline.Token).ConfigureAwait(false);
                            if (count == 0)
                                throw new EndOfStreamException("상대가 TCP 연결을 종료했습니다.");
                            incoming.Advance(count);
                        }
                    }
                }
            }
            finally { Volatile.Write(ref busy, 0); }
        }
        public async Task SendAsync(byte[] message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            Enter();
            try
            {
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop))
                {
                    deadline.CancelAfter(timeout);
                    using (deadline.Token.Register(Dispose)) await stream.WriteAsync(message, 0, message.Length, deadline.Token).ConfigureAwait(false);
                }
            }
            finally { Volatile.Write(ref busy, 0); }
        }
        private void Enter()
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(TcpServerConnection));
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
                throw new InvalidOperationException("연결 송수신은 순차적으로 await해야 합니다.");
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
                socket.Dispose();
        }
    }
}
