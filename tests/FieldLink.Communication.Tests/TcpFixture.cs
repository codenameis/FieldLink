using FieldLink.Communication;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using System.Net;
using System.Net.Sockets;

namespace FieldLink.Communication.Tests;

internal sealed class TcpFixture : IDisposable
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);
    internal TcpListener Listener { get; }
    internal ITcpClient Client { get; }
    internal Socket Peer { get; }

    private TcpFixture(TcpListener listener, ITcpClient client, Socket peer)
    {
        Listener = listener;
        Client = client;
        Peer = peer;
    }

    internal static async Task<TcpFixture> CreateAsync(int maximumFrameLength = 1024 * 1024, IPAddress? address = null)
    {
        address ??= IPAddress.Loopback;
        var listener = new TestTcpListener(address, 0);
        listener.Start();
        var client = new TcpClient((IPEndPoint)listener.LocalEndpoint, new FieldLink.Communication.Framing.FixedLengthFrame(1), maximumFrameLength, new IPEndPoint(address, 0));
        try
        {
            await client.OpenAsync(Timeout);
            var peer = await listener.AcceptSocketAsync();
            peer.NoDelay = true;
            return new TcpFixture(listener, client, peer);
        }
        catch { client.Dispose(); listener.Stop(); throw; }
    }

    internal static async Task<byte[]> ReadExactlyAsync(Socket socket, int length)
    {
        byte[] data = new byte[length];
        int received = 0;
        while (received < length)
        {
            int count = await socket.ReceiveAsync(new ArraySegment<byte>(data, received, data.Length - received), SocketFlags.None);
            if (count == 0)
                throw new EndOfStreamException($"Expected {length}; received {received}");
            received += count;
        }
        return data;
    }

    internal static async Task WriteAsync(Socket socket, byte[] data)
    {
        int sent = 0;
        while (sent < data.Length) sent += await socket.SendAsync(new ArraySegment<byte>(data, sent, data.Length - sent), SocketFlags.None);
    }

    public void Dispose()
    {
        Client.Dispose();
        Peer.Dispose();
        Listener.Stop();
    }
}
