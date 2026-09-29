using FieldLink.Communication.Diagnostics;
using SocketUdpClient = System.Net.Sockets.UdpClient;
using FieldLink.Communication;
using UdpClient = FieldLink.Communication.Udp.UdpClient;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FieldLink.Communication.Tests;

internal static class UdpTests
{
    private static SocketUdpClient CreatePeer(IPAddress? address = null) => new SocketUdpClient(new IPEndPoint(address ?? IPAddress.Loopback, 0));
    private static IPEndPoint Address(SocketUdpClient peer) => (IPEndPoint)peer.Client.LocalEndPoint!;

    internal static async Task RoundTripAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync(TcpFixture.Timeout);
        Task<byte[]> response = client.ExchangeAsync([1, 2], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        TestAssert.Bytes([1, 2], received.Buffer);
        await peer.SendAsync(new byte[] { 3, 4 }, received.RemoteEndPoint);
        TestAssert.Bytes([3, 4], await response);
    }

    internal static async Task ForeignSenderAndOldSidAsync()
    {
        using var peer = CreatePeer();
        using var stranger = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer), 10);
        await client.OpenAsync();
        byte[] request = new byte[10];
        request[9] = 7;
        Task<byte[]> response = client.ExchangeAsync(request, TcpFixture.Timeout,
            data => data.Length < 10 ? ResponseDisposition.Reject :
                data[9] == request[9] ? ResponseDisposition.Accept : ResponseDisposition.Ignore);
        UdpReceiveResult received = await peer.ReceiveAsync();
        await stranger.SendAsync(new byte[1000], received.RemoteEndPoint);
        await stranger.SendAsync(request, received.RemoteEndPoint);
        await Task.Delay(40);
        TestAssert.True(!response.IsCompleted, "A foreign sender must not satisfy the request");
        byte[] previous = (byte[])request.Clone();
        previous[9] = 6;
        await peer.SendAsync(previous, received.RemoteEndPoint);
        await Task.Delay(40);
        TestAssert.True(!response.IsCompleted, "Previous SID must be ignored");
        await peer.SendAsync(request, received.RemoteEndPoint);
        TestAssert.Bytes(request, await response);
    }

    internal static async Task DatagramBoundaryAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> response = client.ExchangeAsync([1], TcpFixture.Timeout, data => data[0] == 2 ? ResponseDisposition.Accept : ResponseDisposition.Ignore);
        UdpReceiveResult received = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 1 }, received.RemoteEndPoint);
        await peer.SendAsync(new byte[] { 2, 3 }, received.RemoteEndPoint);
        TestAssert.Bytes([2, 3], await response);
    }

    internal static async Task ZeroLengthDatagramAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> response = client.ExchangeAsync([], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        TestAssert.Equal(0, received.Buffer.Length);
        await peer.SendAsync(Array.Empty<byte>(), received.RemoteEndPoint);
        TestAssert.Equal(0, (await response).Length);
    }

    internal static async Task OversizedDatagramAsync()
    {
        foreach (int size in new[] { 5, 100 })
        {
            using var peer = CreatePeer();
            using IUdpClient client = new UdpClient(Address(peer), 4);
            await client.OpenAsync();
            Task<byte[]> response = client.ExchangeAsync([1], TcpFixture.Timeout);
            UdpReceiveResult received = await peer.ReceiveAsync();
            await peer.SendAsync(new byte[size], received.RemoteEndPoint);
            await TestAssert.FailureAsync(() => response, CommunicationFailure.MessageTooLarge);
        }
    }

    internal static async Task TimeoutDoesNotRetryAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> response = client.ExchangeAsync([1], TimeSpan.FromMilliseconds(120));
        await peer.ReceiveAsync();
        await TestAssert.FailureAsync(() => response, CommunicationFailure.Timeout);
        using var noMorePackets = new CancellationTokenSource(150);
        await TestAssert.ThrowsAsync<OperationCanceledException>(async () => await peer.ReceiveAsync(noMorePackets.Token));
    }

    internal static async Task CancellationAndReuseAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> first = client.ExchangeAsync([1], TcpFixture.Timeout, cancellationToken: cancellation.Token);
        await peer.ReceiveAsync();
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => first, CommunicationFailure.Cancelled);
        await client.OpenAsync();
        Task<byte[]> second = client.ExchangeAsync([2], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        await peer.SendAsync(received.Buffer, received.RemoteEndPoint);
        TestAssert.Bytes([2], await second);
    }

    internal static async Task ConcurrentRequestsAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> first = client.ExchangeAsync([1], TcpFixture.Timeout);
        Task<byte[]> second = client.ExchangeAsync([2], TcpFixture.Timeout);
        for (byte expected = 1; expected <= 2; expected++)
        {
            UdpReceiveResult request = await peer.ReceiveAsync();
            TestAssert.Bytes([expected], request.Buffer);
            await peer.SendAsync(request.Buffer, request.RemoteEndPoint);
        }
        TestAssert.Bytes([1], await first);
        TestAssert.Bytes([2], await second);
    }

    internal static async Task QueueCancellationAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> first = client.ExchangeAsync([1], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> second = client.ExchangeAsync([2], TcpFixture.Timeout, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var error = await TestAssert.FailureAsync(() => second, CommunicationFailure.Cancelled);
        TestAssert.Equal(CommunicationStage.Waiting, error.Stage);
        await peer.SendAsync(received.Buffer, received.RemoteEndPoint);
        TestAssert.Bytes([1], await first);
    }

    internal static async Task DisposeAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> first = client.ExchangeAsync([1], TcpFixture.Timeout);
        await peer.ReceiveAsync();
        Task<byte[]> second = client.ExchangeAsync([2], TcpFixture.Timeout);
        client.Dispose();
        await TestAssert.FailureAsync(() => first, CommunicationFailure.Disposed);
        await TestAssert.FailureAsync(() => second, CommunicationFailure.Disposed);
    }

    internal static async Task LocalBindingAndSendOnlyAsync()
    {
        using var peer = CreatePeer();
        using var reservation = CreatePeer();
        IPEndPoint local = Address(reservation);
        reservation.Dispose();
        using IUdpClient client = new UdpClient(Address(peer), localEndPoint: local);
        await client.OpenAsync();
        await client.SendAsync([9], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        TestAssert.Equal(local.Port, received.RemoteEndPoint.Port);
        TestAssert.Bytes([9], received.Buffer);
    }

    internal static async Task IgnoredResponsesDeadlineAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        var watch = Stopwatch.StartNew();
        Task<byte[]> response = client.ExchangeAsync([1], TimeSpan.FromMilliseconds(200), _ => ResponseDisposition.Ignore);
        UdpReceiveResult received = await peer.ReceiveAsync();
        for (int index = 0; index < 3; index++)
        {
            await peer.SendAsync(new byte[] { 9 }, received.RemoteEndPoint);
            await Task.Delay(40);
        }
        await TestAssert.FailureAsync(() => response, CommunicationFailure.Timeout);
        TestAssert.True(watch.Elapsed < TimeSpan.FromMilliseconds(900));
    }

    internal static async Task RejectedResponseAsync()
    {
        using var peer = CreatePeer();
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> response = client.ExchangeAsync([1], TcpFixture.Timeout, _ => ResponseDisposition.Reject);
        UdpReceiveResult received = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 9 }, received.RemoteEndPoint);
        await TestAssert.FailureAsync(() => response, CommunicationFailure.ResponseRejected);
    }

    internal static async Task Ipv6Async()
    {
        using var peer = CreatePeer(IPAddress.IPv6Loopback);
        using IUdpClient client = new UdpClient(Address(peer));
        await client.OpenAsync();
        Task<byte[]> response = client.ExchangeAsync([1], TcpFixture.Timeout);
        UdpReceiveResult received = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 2 }, received.RemoteEndPoint);
        TestAssert.Bytes([2], await response);
    }
}
