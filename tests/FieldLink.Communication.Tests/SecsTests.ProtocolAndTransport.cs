using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FieldLink.Secs;
using FieldLink.Secs.Types;
using FieldLink.Secs.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class SecsTests
{
    internal static Task AllNumericFormatsMatchFixedBytesAsync()
    {
        (SecsValue Value, string Hex)[] vectors =
        [
            (new SecsValue((sbyte)-1), "6501FF"),
            (new SecsValue((byte)255), "A501FF"),
            (new SecsValue((short)-2), "6902FFFE"),
            (new SecsValue((ushort)0x1234), "A9021234"),
            (new SecsValue(-2), "7104FFFFFFFE"),
            (new SecsValue(0x12345678u), "B10412345678"),
            (new SecsValue(-2L), "6108FFFFFFFFFFFFFFFE"),
            (new SecsValue(0x123456789abcdef0UL), "A108123456789ABCDEF0"),
            (new SecsValue(1.0f), "91043F800000"),
            (new SecsValue(1.0), "81083FF0000000000000"),
            (new SecsValue(new bool[] { true, false }), "2502FF00"),
            (new SecsValue(SecsItemType.JIS8, new byte[] { 0x41, 0x42 }), "45024142"),
            (new SecsValue(new int[] { 1, -1 }), "710800000001FFFFFFFF"),
            (new SecsValue(SecsItemType.Byte, new byte[] { 1, 255 }), "A50201FF"),
        ];
        foreach (var vector in vectors)
        {
            byte[] expected = TestBytes.FromHexString(vector.Hex);
            TestAssert.Bytes(expected, vector.Value.ToSourceBytes());
            TestAssert.Bytes(expected, SecsValue.ParseFromSource(expected, Encoding.ASCII).ToSourceBytes());
            TestAssert.Bytes(expected, new SecsValue(vector.Value.ToXElement()).ToSourceBytes());
        }
        var wrapped = new SecsValue(new object[] { (byte)4, (sbyte)-1, new short[] { 1, 2 } });
        TestAssert.Equal(3, wrapped.Length);
        TestAssert.Equal(SecsItemType.Byte, ((SecsValue[])wrapped.Value)[0].ItemType);
        return Task.CompletedTask;
    }

    internal static Task ItemLengthBoundariesAndXmlCultureAsync()
    {
        foreach (int length in new[] { 0, 1, 255, 256, 65535, 65536 })
        {
            var item = new SecsValue(new byte[length]);
            byte[] bytes = item.ToSourceBytes();
            int size = length < 256 ? 1 : length < 65536 ? 2 : 3;
            TestAssert.Equal((byte)(0x20 | size), bytes[0]);
            int encodedLength = 0;
            for (int i = 0; i < size; i++)
                encodedLength = encodedLength * 256 + bytes[i + 1];
            TestAssert.Equal(length, encodedLength);
            TestAssert.Equal(length, SecsValue.ParseFromSource(bytes, Encoding.ASCII).Length);
        }
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            foreach (var value in new[] { new SecsValue(new float[] { 1.5f }), new SecsValue(new double[0]), new SecsValue(new[] { "A", "B" }) })
                TestAssert.Bytes(value.ToSourceBytes(), new SecsValue(value.ToXElement()).ToSourceBytes());
            TestAssert.Bytes(TestBytes.FromHexString("4103EAB080"), new SecsValue("가").ToSourceBytes(Encoding.UTF8));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        return Task.CompletedTask;
    }

    internal static async Task NestedAndMutableItemsCannotCrashDecoderAsync()
    {
        var bytes = new List<byte>();
        for (int i = 0; i < 66; i++)
            bytes.AddRange(new byte[] { 1, 1 });
        bytes.AddRange(new byte[] { 1, 0 });
        await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(SecsValue.ParseFromSource(bytes.ToArray(), Encoding.ASCII)));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(new SecsValue(new object[] { new object() })));
        var list = SecsValue.EmptyListValue();
        var cyclic = new SecsValue(new object[] { list });
        ((SecsValue[])cyclic.Value)[0] = cyclic;
        await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(cyclic.ToSourceBytes()));
    }

    internal static Task SecsOneReferenceSplittingAndChecksumAsync()
    {
        var block = Secs1MessageBuilder.Build(1, 1, 1, 1, 0x12345678, new byte[] { 0x41, 0x42 }, true);
        TestAssert.Bytes(TestBytes.FromHexString("0C000181018001123456784142029B"), block[0]);
        TestAssert.Equal(0, Secs1MessageBuilder.Build(1, 1, 1, 1, 0, Array.Empty<byte>(), true).Count);
        foreach (int count in new[] { 244, 245, 448, 449 })
        {
            var data = Enumerable.Range(0, count).Select(i => (byte)i).ToArray();
            var blocks = Secs1MessageBuilder.Build(1, 1, 1, 9, 4, data, false);
            int chunk = count <= 244 ? 244 : 224;
            TestAssert.Equal((count + chunk - 1) / chunk, blocks.Count);
            for (int i = 0; i < blocks.Count; i++)
            {
                byte[] part = blocks[i];
                TestAssert.Equal((byte)9, part[6]);
                TestAssert.Equal(i == blocks.Count - 1, (part[5] & 128) != 0);
                int sum = part.Skip(1).Take(part.Length - 3).Sum(b => (int)b) & 65535;
                TestAssert.Equal(sum, part[part.Length - 2] * 256 + part[part.Length - 1]);
            }
            TestAssert.Bytes(data, blocks.SelectMany(b => b.Skip(11).Take(b.Length - 13)).ToArray());
        }
        return Task.CompletedTask;
    }

    internal static async Task SelectGemConcurrentRequestsAndPushAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        server.MessageReceived += (session, request) =>
        {
            SecsValue value = request.FunctionNo == 1 && request.StreamNo == 1 ? new OnlineData("MODEL", "1.0") :
                request.FunctionNo == 13 && request.StreamNo == 1 ? new SecsValue(new object[] { new byte[] { 0 }, (SecsValue)new OnlineData("MODEL", "1.0") }) :
                new SecsValue(new byte[] { (byte)request.MessageID });
            session.ReplyAsync(request, value).GetAwaiter().GetResult();
        };
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint, 0x8123, selectOnOpen: true);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.RequestAsync(1, 1, new SecsValue()));
        await client.OpenAsync();
        TestAssert.True(client.Session.IsSelected);
        await client.Session.LinkTestAsync();
        TestAssert.Equal("MODEL", (await client.Gem.AreYouThereAsync()).ModelType);
        TestAssert.Equal("1.0", (await client.Gem.EstablishCommunicationsAsync()).SoftVersion);
        var replies = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => client.RequestAsync(3, 1, new SecsValue())));
        TestAssert.Equal(25, replies.Select(r => r.MessageID).Distinct().Count());
        foreach (var reply in replies)
            TestAssert.Equal((byte)reply.MessageID, ((byte[])reply.GetItemValues().Value)[0]);
        var pushed = new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += (_, message) => pushed.TrySetResult(message);
        await server.PublishAsync(6, 11, new SecsValue("event"));
        TestAssert.Equal("event", (string)(await pushed.Task).GetItemValues().Value);
        TestAssert.True(client.Session.LastMessageHandlerError == null);
        await client.CloseAsync();
        await server.StopAsync();
    }

    internal static async Task AutomaticRepliesPreserveSystemBytesAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        var connected = new TaskCompletionSource<HsmsSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (session, _) => connected.TrySetResult(session);
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint, 1);
        await client.OpenAsync();
        await client.Session.SendAsync(6, 11, new SecsValue());
        HsmsSession peer = await connected.Task;
        TestAssert.Equal("", (await peer.Gem.AreYouThereAsync()).ModelType);
        TestAssert.Equal("", (await peer.Gem.EstablishCommunicationsAsync()).SoftVersion);
        var time = await peer.RequestAsync(2, 17, new SecsValue());
        TestAssert.Equal(16, ((string)time.GetItemValues().Value).Length);
        await client.CloseAsync();
        await server.StopAsync();
    }

    internal static async Task TimeoutCancellationAndLateRepliesKeepOtherRequestsAliveAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        var requests = new System.Collections.Concurrent.ConcurrentQueue<(HsmsSession Session, SecsMessage Message)>();
        var received = new SemaphoreSlim(0);
        server.MessageReceived += (session, message) => { requests.Enqueue((session, message)); received.Release(); };
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint, timeout: TimeSpan.FromMilliseconds(400), automaticGemReplies: false);
        await client.OpenAsync();
        var timedOut = client.RequestAsync(3, 1, new SecsValue());
        await received.WaitAsync();
        TestAssert.True(requests.TryDequeue(out var old), "First request must be queued.");
        await TestAssert.ThrowsAsync<TimeoutException>(async () => await timedOut);
        var next = client.RequestAsync(3, 1, new SecsValue());
        await received.WaitAsync();
        TestAssert.True(requests.TryDequeue(out var fresh), "Second request must be queued.");
        await old.Session.ReplyAsync(old.Message, new SecsValue("old"));
        TestAssert.True(!next.IsCompleted, "Late response must not complete the next request.");
        await fresh.Session.ReplyAsync(fresh.Message, new SecsValue("new"));
        TestAssert.Equal("new", (string)(await next).GetItemValues().Value);
        using var cancel = new CancellationTokenSource();
        var cancelled = client.RequestAsync(3, 1, new SecsValue(), cancel.Token);
        await received.WaitAsync();
        cancel.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(async () => await cancelled);
        TestAssert.True(client.IsOpen, "Cancellation after completed send must preserve the session.");
        await client.Session.LinkTestAsync();
        await client.CloseAsync();
        await server.StopAsync();
    }

    internal static async Task CloseReconnectAndCallbackReentryAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0), automaticGemReplies: true);
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint);
        await client.OpenAsync();
        HsmsSession old = client.Session;
        long generation = client.ConnectionGeneration;
        var pending = client.RequestAsync(3, 1, new SecsValue());
        await client.CloseAsync();
        await TestAssert.ThrowsAsync<IOException>(async () => await pending);
        await client.OpenAsync();
        TestAssert.True(client.ConnectionGeneration > generation);
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => old.LinkTestAsync());
        // TCP connect completion alone does not mean the server has accepted the new session.
        await client.Session.LinkTestAsync();
        var callback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += (session, _) =>
        {
            try
            {
                session.Gem.AreYouThereAsync().GetAwaiter().GetResult();
                client.CloseAsync().GetAwaiter().GetResult();
                callback.TrySetResult(true);
            }
            catch (Exception error) { callback.TrySetException(error); }
        };
        await server.PublishAsync(6, 11, new SecsValue());
        await callback.Task;
        TestAssert.True(!client.IsOpen);
        await server.StopAsync();
        await server.StartAsync();
        await server.StopAsync();
        client.Dispose();
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
    }

    internal static async Task FragmentedCoalescedFramesAndWrongIdentityAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new HsmsClient((IPEndPoint)listener.LocalEndpoint, 0x9234, timeout: TimeSpan.FromSeconds(2));
            var accept = listener.AcceptTcpClientAsync();
            await client.OpenAsync();
            using var raw = await accept;
            using var stream = raw.GetStream();
            var requestTask = client.RequestAsync(3, 1, new SecsValue());
            var request = HsmsCodec.Decode(await ReadRawFrameAsync(stream));
            TestAssert.Equal((ushort)0x9234, request.DeviceID);
            byte[] wrong = HsmsCodec.Encode(new SecsMessage(0x1234, 3, 2, request.MessageID, new SecsValue("bad").ToSourceBytes()));
            byte[] control = HsmsCodec.Encode(new SecsMessage(65535, 0, 0, 77, sessionType: 5));
            byte[] right = HsmsCodec.Encode(new SecsMessage(request.DeviceID, 3, 2, request.MessageID, new SecsValue("good").ToSourceBytes()));
            var packet = wrong.Concat(control).Concat(right).ToArray();
            await stream.WriteAsync(packet, 0, 2);
            await stream.WriteAsync(packet, 2, 5);
            await stream.WriteAsync(packet, 7, packet.Length - 7);
            var reply = await requestTask;
            TestAssert.Equal("good", (string)reply.GetItemValues().Value);
            var link = HsmsCodec.Decode(await ReadRawFrameAsync(stream));
            TestAssert.Equal((byte)6, link.SessionType);
            TestAssert.Equal(77u, link.MessageID);
            await client.CloseAsync();
        }
        finally { listener.Stop(); }
    }

    internal static async Task OversizedFramesAbortPendingRequestsAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new HsmsClient((IPEndPoint)listener.LocalEndpoint, maximumFrameLength: 128);
            var accept = listener.AcceptTcpClientAsync();
            await client.OpenAsync();
            using var raw = await accept;
            var session = client.Session;
            var request = client.RequestAsync(1, 1, new SecsValue());
            await ReadRawFrameAsync(raw.GetStream());
            await raw.GetStream().WriteAsync(new byte[] { 0, 0, 1, 0 }, 0, 4);
            await TestAssert.ThrowsAsync<IOException>(async () => await request);
            await session.Completion;
            TestAssert.True(!client.IsOpen);
            TestAssert.True(session.LastError != null);
        }
        finally { listener.Stop(); }
    }

    private static async Task<byte[]> ReadRawFrameAsync(NetworkStream stream)
    {
        byte[] header = new byte[4];
        await ReadExactAsync(stream, header, 0, 4);
        int count = header[0] * 16777216 + header[1] * 65536 + header[2] * 256 + header[3];
        TestAssert.True(count >= 10 && count <= 1024 * 1024);
        byte[] frame = new byte[count + 4];
        header.CopyTo(frame, 0);
        await ReadExactAsync(stream, frame, 4, count);
        return frame;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = await stream.ReadAsync(buffer, offset, count);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
            count -= read;
        }
    }
}
