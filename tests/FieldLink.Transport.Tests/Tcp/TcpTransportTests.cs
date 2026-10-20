using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;
using FieldLink.Transport.Tcp;
using Xunit;

namespace FieldLink.Transport.Tests.Tcp;

public sealed class TcpTransportTests
{
    [Fact]
    public void Options_Validation_ThrowsForInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => new TcpTransportOptions("", 502));
        Assert.Throws<ArgumentException>(() => new TcpTransportOptions("   ", 502));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransportOptions("127.0.0.1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TcpTransportOptions("127.0.0.1", 65536));
    }

    [Fact]
    public async Task RunExclusiveAsync_BeforeOpen_ThrowsInvalidOperationException()
    {
        var options = new TcpTransportOptions("127.0.0.1", 502);
        using var transport = new TcpTransport(options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.RunExclusiveAsync(session => Task.FromResult(0)));
    }

    [Fact]
    public async Task Loopback_SendAndReceive_CompletesSuccessfully()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        // 에코 서버 비동기 실행 (CRLF 수신 후 동일하게 응답)
        Task echoServerTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream serverStream = serverClient.GetStream();
            byte[] buffer = new byte[256];
            int readBytes = await serverStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            await serverStream.WriteAsync(buffer, 0, readBytes).ConfigureAwait(false);
            await serverStream.FlushAsync().ConfigureAwait(false);
        });

        try
        {
            var options = new TcpTransportOptions("127.0.0.1", port);
            using var transport = new TcpTransport(options);

            await transport.OpenAsync(CancellationToken.None);
            Assert.Equal(TransportState.Open, transport.State);

            var decoder = new DelimitedFrameDecoder(new byte[] { 0x0D, 0x0A });
            byte[] request = new byte[] { 0x48, 0x69, 0x0D, 0x0A }; // "Hi\r\n"

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
            listener.Stop();
            await echoServerTask;
        }
    }
}
