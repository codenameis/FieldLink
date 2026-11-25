using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Udp;
using Xunit;

namespace FieldLink.Transport.Tests.Udp;

public sealed class UdpListenerTransportTests
{
    [Fact]
    public void Options_Validation_ThrowsForOutOfRangePort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdpListenerTransportOptions(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdpListenerTransportOptions(65536));
        Assert.Throws<ArgumentNullException>(() => new UdpListenerTransportOptions(7127, null!));

        var ephemeral = new UdpListenerTransportOptions(0, IPAddress.Loopback);
        Assert.Equal(0, ephemeral.LocalPort);
        Assert.Equal(IPAddress.Loopback, ephemeral.LocalAddress);
    }

    [Fact]
    public async Task ReceiveAndSend_BeforeOpen_ThrowsInvalidOperationException()
    {
        using var listener = new UdpListenerTransport(new UdpListenerTransportOptions(0, IPAddress.Loopback));

        await Assert.ThrowsAsync<InvalidOperationException>(() => listener.ReceiveAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => listener.SendToAsync(new byte[] { 0x01 }, new IPEndPoint(IPAddress.Loopback, 7127), CancellationToken.None));
    }

    [Fact]
    public async Task Loopback_ReceiveFromRobotAndReply_Succeeds()
    {
        using var listener = new UdpListenerTransport(new UdpListenerTransportOptions(0, IPAddress.Loopback));
        await listener.OpenAsync(CancellationToken.None);

        Assert.Equal(TransportState.Open, listener.State);
        Assert.True(listener.BoundPort > 0);

        using var robotSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        byte[] outbound = new byte[] { 0x53, 0x00, 0x00, 0x00 };

        await robotSender.SendAsync(outbound, outbound.Length, new IPEndPoint(IPAddress.Loopback, listener.BoundPort));

        UdpDatagram datagram = await listener.ReceiveAsync(CancellationToken.None);
        Assert.Equal(outbound, datagram.GetPayload());

        byte[] reply = new byte[] { 0x53, 0x01, 0x02, 0x03 };
        await listener.SendToAsync(reply, datagram.RemoteEndPoint, CancellationToken.None);

        UdpReceiveResult receivedReply = await robotSender.ReceiveAsync();
        Assert.Equal(reply, receivedReply.Buffer);

        await listener.CloseAsync(CancellationToken.None);
        Assert.Equal(TransportState.Closed, listener.State);
    }

    [Fact]
    public async Task ReceiveAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        using var listener = new UdpListenerTransport(new UdpListenerTransportOptions(0, IPAddress.Loopback));
        await listener.OpenAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.ReceiveAsync(cts.Token));
    }

    [Fact]
    public async Task ReceiveAsync_ConcurrentCallers_NeverDeliverDuplicateDatagram()
    {
        using var listener = new UdpListenerTransport(new UdpListenerTransportOptions(0, IPAddress.Loopback));
        await listener.OpenAsync(CancellationToken.None);

        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destination = new IPEndPoint(IPAddress.Loopback, listener.BoundPort);

        Task<UdpDatagram> firstReceive = listener.ReceiveAsync(CancellationToken.None);
        Task<UdpDatagram> secondReceive = listener.ReceiveAsync(CancellationToken.None);
        await Task.Delay(30);

        await sender.SendAsync(new byte[] { 0x11 }, 1, destination);
        await sender.SendAsync(new byte[] { 0x22 }, 1, destination);

        UdpDatagram first = await firstReceive;
        UdpDatagram second = await secondReceive;

        byte[] payloads = { first.GetPayload()[0], second.GetPayload()[0] };
        Array.Sort(payloads);
        Assert.Equal(new byte[] { 0x11, 0x22 }, payloads);
    }

    [Fact]
    public async Task Reopen_AfterCloseDuringPendingReceive_DoesNotPoisonNewConnection()
    {
        using var listener = new UdpListenerTransport(new UdpListenerTransportOptions(0, IPAddress.Loopback));
        await listener.OpenAsync(CancellationToken.None);

        Task<UdpDatagram> staleReceive = listener.ReceiveAsync(CancellationToken.None);
        await Task.Delay(30);
        await listener.CloseAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => staleReceive);

        await listener.OpenAsync(CancellationToken.None);
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await sender.SendAsync(new byte[] { 0x5A, 0xA5 }, 2, new IPEndPoint(IPAddress.Loopback, listener.BoundPort));

        UdpDatagram fresh = await listener.ReceiveAsync(CancellationToken.None);
        Assert.Equal(new byte[] { 0x5A, 0xA5 }, fresh.GetPayload());
    }
}
