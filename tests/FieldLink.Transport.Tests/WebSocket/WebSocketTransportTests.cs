using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.WebSocket;
using Xunit;

namespace FieldLink.Transport.Tests.WebSocket;

public sealed class WebSocketTransportTests
{
    [Fact]
    public void Options_Validation_ThrowsForInvalidInputs()
    {
        Assert.Throws<ArgumentNullException>(() => new WebSocketTransportOptions(null!));
        Assert.Throws<ArgumentException>(() => new WebSocketTransportOptions(new Uri("http://127.0.0.1:8080")));
        Assert.Throws<ArgumentException>(() => new WebSocketTransportOptions(new Uri("/relative", UriKind.Relative)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080")) { ConnectTimeout = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080")) { ConnectTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080")) { KeepAliveInterval = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080")) { MaximumMessageSize = 0 });
        Assert.Throws<ArgumentException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080"))
        {
            Headers = new Dictionary<string, string> { ["X-Header"] = "bad\r\nvalue" }
        });
        Assert.Throws<ArgumentException>(() => new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080"))
        {
            SubProtocols = new[] { "proto\r\n" }
        });
    }

    [Fact]
    public async Task SendAndReceive_BeforeOpen_ThrowsInvalidOperationException()
    {
        var options = new WebSocketTransportOptions(new Uri("ws://127.0.0.1:8080"));
        using var transport = new WebSocketTransport(options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.SendAsync(new byte[] { 0x01 }, WebSocketMessageKind.Text, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.ReceiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Loopback_TextBinaryAndFragmentedMessages_RoundtripAndClose()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task serverTask = Task.Run(async () =>
        {
            using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream stream = client.GetStream();

            await PerformServerHandshakeAsync(stream).ConfigureAwait(false);

            // 1. 클라이언트가 보낸 텍스트 프레임 읽기 후, 두 개 프래그먼트로 쪼개서 에코
            (int opcode1, byte[] payload1) = await ReadClientFrameAsync(stream).ConfigureAwait(false);
            Assert.Equal(0x1, opcode1);
            int half = payload1.Length / 2;
            await WriteServerFrameAsync(stream, fin: false, opcode: 0x1, payload1[..half]).ConfigureAwait(false);
            await WriteServerFrameAsync(stream, fin: true, opcode: 0x0, payload1[half..]).ConfigureAwait(false);

            // 2. 바이너리 프레임 에코
            (int opcode2, byte[] payload2) = await ReadClientFrameAsync(stream).ConfigureAwait(false);
            Assert.Equal(0x2, opcode2);
            await WriteServerFrameAsync(stream, fin: true, opcode: 0x2, payload2).ConfigureAwait(false);

            // 3. 종료 프레임 수신 시 Close 응답
            (int closeOpcode, _) = await ReadClientFrameAsync(stream).ConfigureAwait(false);
            Assert.Equal(0x8, closeOpcode);
            await WriteServerFrameAsync(stream, fin: true, opcode: 0x8, new byte[] { 0x03, 0xE8 }).ConfigureAwait(false);
        });

        try
        {
            var options = new WebSocketTransportOptions(new Uri($"ws://127.0.0.1:{port}/ws"));
            using var transport = new WebSocketTransport(options);

            await transport.OpenAsync(CancellationToken.None);
            Assert.Equal(TransportState.Open, transport.State);

            byte[] textBytes = Encoding.UTF8.GetBytes("{\"cmd\":\"status\"}");
            await transport.SendAsync(textBytes, WebSocketMessageKind.Text, CancellationToken.None);
            WebSocketFrameMessage textReply = await transport.ReceiveAsync(CancellationToken.None);
            Assert.Equal(WebSocketMessageKind.Text, textReply.Kind);
            Assert.Equal(textBytes, textReply.GetPayload());

            byte[] binaryBytes = new byte[] { 0x10, 0x20, 0x30, 0x40 };
            await transport.SendAsync(binaryBytes, WebSocketMessageKind.Binary, CancellationToken.None);
            WebSocketFrameMessage binaryReply = await transport.ReceiveAsync(CancellationToken.None);
            Assert.Equal(WebSocketMessageKind.Binary, binaryReply.Kind);
            Assert.Equal(binaryBytes, binaryReply.GetPayload());

            await transport.CloseAsync(CancellationToken.None);
            Assert.Equal(TransportState.Closed, transport.State);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    [Fact]
    public async Task ReceiveAndCloseConcurrently_TransitionsStateCleanlyWithoutConcurrencyError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task serverTask = Task.Run(async () =>
        {
            using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            using NetworkStream stream = client.GetStream();
            await PerformServerHandshakeAsync(stream).ConfigureAwait(false);
            try
            {
                await ReadClientFrameAsync(stream).ConfigureAwait(false);
            }
            catch
            {
            }
        });

        try
        {
            var options = new WebSocketTransportOptions(new Uri($"ws://127.0.0.1:{port}/ws"));
            using var transport = new WebSocketTransport(options);
            await transport.OpenAsync(CancellationToken.None);

            Task<WebSocketFrameMessage> receiveTask = transport.ReceiveAsync(CancellationToken.None);
            await Task.Delay(40);
            await transport.CloseAsync(CancellationToken.None);

            Assert.Equal(TransportState.Closed, transport.State);
            TransportException receiveException = await Assert.ThrowsAsync<TransportException>(() => receiveTask);
            Assert.Equal(TransportFailure.ConnectionClosed, receiveException.Failure);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    [Fact]
    public async Task OpenAsync_WhenSocketConfigurationThrows_ResetsStateToClosedInsteadOfStuckOpening()
    {
        var options = new WebSocketTransportOptions(new Uri("ws://127.0.0.1:1/ws"))
        {
            Headers = new Dictionary<string, string> { ["Host: bad"] = "val" }
        };
        using var transport = new WebSocketTransport(options);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.OpenAsync(CancellationToken.None));
        Assert.Equal(TransportState.Closed, transport.State);
    }

    private static async Task PerformServerHandshakeAsync(NetworkStream stream)
    {
        var headerBytes = new List<byte>();
        byte[] singleByte = new byte[1];
        while (true)
        {
            int read = await stream.ReadAsync(singleByte, 0, 1).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            headerBytes.Add(singleByte[0]);
            int count = headerBytes.Count;
            if (count >= 4
                && headerBytes[count - 4] == '\r'
                && headerBytes[count - 3] == '\n'
                && headerBytes[count - 2] == '\r'
                && headerBytes[count - 1] == '\n')
            {
                break;
            }
        }

        string requestText = Encoding.ASCII.GetString(headerBytes.ToArray());
        string key = string.Empty;
        foreach (string line in requestText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
            {
                key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                break;
            }
        }

        byte[] sha1 = SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
        string accept = Convert.ToBase64String(sha1);
        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

        byte[] responseBytes = Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(responseBytes, 0, responseBytes.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<(int Opcode, byte[] Payload)> ReadClientFrameAsync(NetworkStream stream)
    {
        byte[] header = await ReadExactAsync(stream, 2).ConfigureAwait(false);
        int opcode = header[0] & 0x0F;
        bool masked = (header[1] & 0x80) != 0;
        int length = header[1] & 0x7F;
        if (length == 126)
        {
            byte[] ext = await ReadExactAsync(stream, 2).ConfigureAwait(false);
            length = (ext[0] << 8) | ext[1];
        }

        byte[] mask = masked ? await ReadExactAsync(stream, 4).ConfigureAwait(false) : Array.Empty<byte>();
        byte[] payload = await ReadExactAsync(stream, length).ConfigureAwait(false);
        if (masked)
        {
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] ^= mask[i % 4];
            }
        }

        return (opcode, payload);
    }

    private static async Task WriteServerFrameAsync(NetworkStream stream, bool fin, int opcode, byte[] payload)
    {
        byte firstByte = (byte)((fin ? 0x80 : 0x00) | (opcode & 0x0F));
        var frame = new List<byte> { firstByte };
        if (payload.Length < 126)
        {
            frame.Add((byte)payload.Length);
        }
        else
        {
            frame.Add(126);
            frame.Add((byte)(payload.Length >> 8));
            frame.Add((byte)(payload.Length & 0xFF));
        }

        frame.AddRange(payload);
        byte[] bytes = frame.ToArray();
        await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }

        return buffer;
    }
}
