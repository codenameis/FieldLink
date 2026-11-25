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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TcpTransportOptions("127.0.0.1", 502) { ConnectTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) });
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

    [Fact]
    public async Task RunExclusiveAsync_WhenReceiveCanceledMidRead_AbortsConnectionPreventingBufferPoisoning()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task serverTask = Task.Run(async () =>
        {
            using TcpClient client1 = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream stream1 = client1.GetStream();
            byte[] req1 = new byte[4];
            await stream1.ReadAsync(req1, 0, req1.Length).ConfigureAwait(false);
            await Task.Delay(120).ConfigureAwait(false);
            try
            {
                await stream1.WriteAsync(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, 0, 4).ConfigureAwait(false);
            }
            catch
            {
            }

            using TcpClient client2 = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream stream2 = client2.GetStream();
            byte[] req2 = new byte[4];
            int read = await stream2.ReadAsync(req2, 0, req2.Length).ConfigureAwait(false);
            await stream2.WriteAsync(req2, 0, read).ConfigureAwait(false);
        });

        try
        {
            using var transport = new TcpTransport(new TcpTransportOptions("127.0.0.1", port));
            await transport.OpenAsync(CancellationToken.None);

            var decoder = new FixedLengthFrameDecoder(4);
            using var cts = new CancellationTokenSource(40);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.RunExclusiveAsync(async session =>
            {
                await session.SendAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
                return await session.ReceiveAsync(decoder);
            }, cts.Token));

            // 고아 ReadAsync 방어를 위해 연결이 Closed로 전환되어 재오픈 후 깨끗한 세션으로 교환된다.
            Assert.Equal(TransportState.Closed, transport.State);
            await transport.OpenAsync(CancellationToken.None);

            byte[] response = await transport.RunExclusiveAsync(async session =>
            {
                await session.SendAsync(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });
                return await session.ReceiveAsync(decoder);
            });
            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, response);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    [Fact]
    public async Task RunExclusiveAsync_DrainPendingReceiveBytes_CapsAt65536BytesWithoutSpinHang()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task serverTask = Task.Run(async () =>
        {
            using TcpClient serverClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream stream = serverClient.GetStream();
            byte[] flood = new byte[80_000];
            await stream.WriteAsync(flood, 0, flood.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            await Task.Delay(200).ConfigureAwait(false);
        });

        try
        {
            using var transport = new TcpTransport(new TcpTransportOptions("127.0.0.1", port));
            await transport.OpenAsync(CancellationToken.None);
            await Task.Delay(50);

            int drainedResult = await transport.RunExclusiveAsync(_ => Task.FromResult(1));
            Assert.Equal(1, drainedResult);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }
}
