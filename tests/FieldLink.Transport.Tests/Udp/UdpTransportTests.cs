using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;
using FieldLink.Transport.Udp;
using Xunit;

namespace FieldLink.Transport.Tests.Udp;

public sealed class UdpTransportTests
{
    [Fact]
    public void Options_Validation_ThrowsForInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => new UdpTransportOptions("", 5000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdpTransportOptions("127.0.0.1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdpTransportOptions("127.0.0.1", 65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdpTransportOptions("127.0.0.1", 5000, localPort: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UdpTransportOptions("127.0.0.1", 5000) { ReceiveTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) });
    }

    [Fact]
    public async Task RunExclusiveAsync_BeforeOpen_ThrowsInvalidOperationException()
    {
        var options = new UdpTransportOptions("127.0.0.1", 5000);
        using var transport = new UdpTransport(options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.RunExclusiveAsync(session => Task.FromResult(0)));
    }

    [Fact]
    public async Task Loopback_SendAndReceive_CompletesSuccessfully()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        Task echoServerTask = Task.Run(async () =>
        {
            UdpReceiveResult received = await server.ReceiveAsync().ConfigureAwait(false);
            await server.SendAsync(received.Buffer, received.Buffer.Length, received.RemoteEndPoint).ConfigureAwait(false);
        });

        try
        {
            var options = new UdpTransportOptions("127.0.0.1", serverPort);
            using var transport = new UdpTransport(options);

            await transport.OpenAsync(CancellationToken.None);
            Assert.Equal(TransportState.Open, transport.State);

            byte[] request = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var decoder = new FixedLengthFrameDecoder(4);

            byte[] response = await transport.RunExclusiveAsync(async session =>
            {
                await session.SendAsync(request);
                return await session.ReceiveAsync(decoder);
            });

            Assert.Equal(request, response);

            await transport.CloseAsync(CancellationToken.None);
            Assert.Equal(TransportState.Closed, transport.State);
        }
        finally
        {
            await echoServerTask;
        }
    }

    [Fact]
    public async Task SendAsync_AfterReceiveTimeout_DiscardsStaleReplyAndReceivesFreshReply()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        Task serverTask = Task.Run(async () =>
        {
            UdpReceiveResult first = await server.ReceiveAsync().ConfigureAwait(false);
            await Task.Delay(120).ConfigureAwait(false);
            await server.SendAsync(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, 4, first.RemoteEndPoint).ConfigureAwait(false);

            UdpReceiveResult second = await server.ReceiveAsync().ConfigureAwait(false);
            await server.SendAsync(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB }, 4, second.RemoteEndPoint).ConfigureAwait(false);
        });

        try
        {
            var options = new UdpTransportOptions("127.0.0.1", serverPort)
            {
                ReceiveTimeout = TimeSpan.FromMilliseconds(50)
            };
            using var transport = new UdpTransport(options);
            await transport.OpenAsync(CancellationToken.None);

            var decoder = new FixedLengthFrameDecoder(4);
            TransportException timeoutException = await Assert.ThrowsAsync<TransportException>(() =>
                transport.RunExclusiveAsync(async session =>
                {
                    await session.SendAsync(new byte[] { 0x01, 0x01, 0x01, 0x01 });
                    return await session.ReceiveAsync(decoder);
                }));
            Assert.IsType<TimeoutException>(timeoutException.InnerException);

            // 지연된 첫 번째 응답이 OS 소켓 버퍼 및 _pendingReceiveTask에 도착할 때까지 대기한다.
            await Task.Delay(150);

            byte[] freshReply = await transport.RunExclusiveAsync(async session =>
            {
                await session.SendAsync(new byte[] { 0x02, 0x02, 0x02, 0x02 });
                return await session.ReceiveAsync(decoder);
            });

            Assert.Equal(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB }, freshReply);
        }
        finally
        {
            await serverTask;
        }
    }

    [Fact]
    public async Task OpenAsync_WithHostname_ResolvesViaAsyncDnsAndRoundtrips()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        Task echoTask = Task.Run(async () =>
        {
            UdpReceiveResult received = await server.ReceiveAsync().ConfigureAwait(false);
            await server.SendAsync(received.Buffer, received.Buffer.Length, received.RemoteEndPoint).ConfigureAwait(false);
        });

        try
        {
            using var transport = new UdpTransport(new UdpTransportOptions("localhost", serverPort));
            await transport.OpenAsync(CancellationToken.None);

            byte[] reply = await transport.RunExclusiveAsync(async session =>
            {
                await session.SendAsync(new byte[] { 0x10, 0x20, 0x30, 0x40 });
                return await session.ReceiveAsync(new FixedLengthFrameDecoder(4));
            });

            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, reply);
        }
        finally
        {
            await echoTask;
        }
    }
}
