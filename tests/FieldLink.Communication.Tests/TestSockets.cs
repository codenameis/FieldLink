using System.Net;
using System.Net.Sockets;

namespace FieldLink.Communication.Tests;

// 가상 상대 전용 어댑터. 취소 시 상대 소켓을 닫고 실제 I/O 종료까지 기다려 유령 수신을 남기지 않는다.
internal static class TestSockets
{
    internal static Task<int> ReceiveAsync(this Socket socket, byte[] data, SocketFlags flags) =>
        socket.ReceiveAsync(new ArraySegment<byte>(data), flags);

    internal static Task<int> ReceiveAsync(this Socket socket, byte[] data, SocketFlags flags, CancellationToken token) =>
        socket.ReceiveAsync(new ArraySegment<byte>(data), flags, token);

    internal static Task<int> SendAsync(this UdpClient peer, byte[] data, IPEndPoint endpoint) =>
        peer.SendAsync(data, data.Length, endpoint);

    internal static Task<int> SendAsync(this UdpClient peer, byte[] data, IPEndPoint endpoint, CancellationToken token) =>
        CancelIoAsync(() => peer.SendAsync(data, data.Length, endpoint), peer.Close, token);

    internal static Task<UdpReceiveResult> ReceiveAsync(this UdpClient peer, CancellationToken token) =>
        CancelIoAsync(peer.ReceiveAsync, peer.Close, token);

    internal static Task<Socket> AcceptSocketAsync(this TcpListener listener, CancellationToken token) =>
        CancelIoAsync(listener.AcceptSocketAsync, listener.Stop, token);

    internal static Task<int> ReceiveAsync(this Socket socket, ArraySegment<byte> data, SocketFlags flags, CancellationToken token) =>
        CancelIoAsync(() => socket.ReceiveAsync(data, flags), socket.Dispose, token);

    internal static Task<int> SendAsync(this Socket socket, ArraySegment<byte> data, SocketFlags flags, CancellationToken token) =>
        CancelIoAsync(() => socket.SendAsync(data, flags), socket.Dispose, token);

    private static async Task<T> CancelIoAsync<T>(Func<Task<T>> operation, Action close, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using (token.Register(close))
        {
            try { return await operation().ConfigureAwait(false); }
            catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        }
    }
}

// .NET Standard 2.0의 TcpListener에는 IDisposable 계약이 없다. using 종료 시 Stop을 보장한다.
internal sealed class TestTcpListener : TcpListener, IDisposable
{
    internal TestTcpListener(IPAddress address, int port) : base(address, port) { }
    internal TestTcpListener(IPEndPoint endpoint) : base(endpoint) { }
    public void Dispose() => Stop();
}
