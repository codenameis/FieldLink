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
}
