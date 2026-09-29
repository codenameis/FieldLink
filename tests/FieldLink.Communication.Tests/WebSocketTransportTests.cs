using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Reflection;
using FieldLink.Communication.WebSockets;
using FieldLink.Communication.Diagnostics;

namespace FieldLink.Communication.Tests;

internal static partial class WebSocketTransportTests
{
    internal static async Task TextAndBinaryRoundTripAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            var text = await peer.ReceiveAsync(token);
            TestAssert.Equal("start", text.Text);
            await peer.SendTextAsync("ready", token);
            var binary = await peer.ReceiveAsync(token);
            TestAssert.Bytes(new byte[] { 1, 2, 3 }, binary.Payload);
            await peer.SendBinaryAsync(binary.Payload, token);
        });
        await server.StartAsync();
        using var client = await WebSocketClient.ConnectAsync(address);
        await client.SendTextAsync("start");
        TestAssert.Equal("ready", (await client.ReceiveAsync()).Text);
        await client.SendBinaryAsync(new byte[] { 1, 2, 3 });
        TestAssert.Bytes(new byte[] { 1, 2, 3 }, (await client.ReceiveAsync()).Payload);
        await server.StopAsync();
    }

    internal static async Task UnauthorizedAndWrongPathAreRejectedAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        int accepted = 0;
        using var server = new WebSocketServer(address,
            request => request.Headers["X-Process-Key"] == "approved",
            (peer, token) => { Interlocked.Increment(ref accepted); return Task.CompletedTask; });
        await server.StartAsync();
        await TestAssert.ThrowsAsync<WebSocketException>(() => WebSocketClient.ConnectAsync(address));
        await TestAssert.ThrowsAsync<WebSocketException>(() => WebSocketClient.ConnectAsync(
            new Uri($"ws://127.0.0.1:{port}/process/other/"),
            configure: options => options.SetRequestHeader("X-Process-Key", "approved")));
        using (var client = await WebSocketClient.ConnectAsync(address,
            configure: options => options.SetRequestHeader("X-Process-Key", "approved")))
            TestAssert.True(client.IsOpen);
        await server.StopAsync();
        TestAssert.Equal(1, accepted);
    }

    internal static async Task FragmentedMessagesAndSizeLimitAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var tooLarge = new TaskCompletionSource<CommunicationFailure>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            try
            {
                var message = await peer.ReceiveAsync(token);
                await peer.SendTextAsync(message.Text, token);
            }
            catch (CommunicationException error) { tooLarge.TrySetResult(error.Failure); }
        }, maximumMessageBytes: 5);
        await server.StartAsync();
        using (var raw = new ClientWebSocket())
        {
            await raw.ConnectAsync(address, CancellationToken.None);
            await raw.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("he")), WebSocketMessageType.Text, false, CancellationToken.None);
            await raw.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("llo")), WebSocketMessageType.Text, true, CancellationToken.None);
            var response = new byte[16];
            var result = await raw.ReceiveAsync(new ArraySegment<byte>(response), CancellationToken.None);
            TestAssert.Equal("hello", Encoding.UTF8.GetString(response, 0, result.Count));
        }
        using (var raw = new ClientWebSocket())
        {
            await raw.ConnectAsync(address, CancellationToken.None);
            await raw.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("123456")), WebSocketMessageType.Text, true, CancellationToken.None);
            TestAssert.Equal(CommunicationFailure.MessageTooLarge, await tooLarge.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        await server.StopAsync();
    }

    internal static async Task ConcurrentSendsAreSerializedAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var received = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            var values = new List<string>();
            for (int i = 0; i < 20; i++) values.Add((await peer.ReceiveAsync(token)).Text);
            received.TrySetResult(values.ToArray());
        });
        await server.StartAsync();
        using var client = await WebSocketClient.ConnectAsync(address);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => client.SendTextAsync(i.ToString())));
        TestAssert.True((await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).OrderBy(value => int.Parse(value))
            .SequenceEqual(Enumerable.Range(0, 20).Select(i => i.ToString())));
        await server.StopAsync();
    }

    internal static async Task CloseAndServerRestartReleaseReceiversAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var remoteClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            entered.TrySetResult(true);
            try { remoteClosed.TrySetResult(await peer.ReceiveAsync(token) == null); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        });
        await server.StartAsync();
        using (var client = await WebSocketClient.ConnectAsync(address))
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await client.CloseAsync();
            TestAssert.True(await remoteClosed.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await server.StartAsync();
        using (var client = await WebSocketClient.ConnectAsync(address))
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task<WebSocketMessage> pending = client.ReceiveAsync();
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.True(!server.IsRunning);
            try { TestAssert.True(await pending.WaitAsync(TimeSpan.FromSeconds(2)) == null); }
            catch (WebSocketException) { }
            TestAssert.True(!client.IsOpen);
        }
    }

    internal static async Task OutboundLimitRejectsBeforeIoAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            var message = await peer.ReceiveAsync(token);
            await peer.SendTextAsync(message.Text, token);
        });
        await server.StartAsync();
        using var client = await WebSocketClient.ConnectAsync(address, maximumMessageBytes: 3);
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendTextAsync("1234"));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendBinaryAsync(new byte[] { 1, 2, 3, 4 }));
        await TestAssert.ThrowsAsync<EncoderFallbackException>(() => client.SendTextAsync("\uD800"));
        await client.SendTextAsync("ok");
        TestAssert.Equal("ok", (await client.ReceiveAsync()).Text);
        await server.StopAsync();
    }

    internal static async Task ConnectionCapRejectsAdditionalPeerAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            entered.TrySetResult(true);
            try { await peer.ReceiveAsync(token); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (WebSocketException) { }
        }, maximumConnections: 1);
        await server.StartAsync();
        using var first = await WebSocketClient.ConnectAsync(address);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await TestAssert.ThrowsAsync<WebSocketException>(() => WebSocketClient.ConnectAsync(address));
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }

    internal static async Task ServerBroadcastsProcessUpdateAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            if (Interlocked.Increment(ref count) == 2) connected.TrySetResult(true);
            try { await peer.ReceiveAsync(token); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (WebSocketException) { }
        });
        await server.StartAsync();
        using var first = await WebSocketClient.ConnectAsync(address);
        using var second = await WebSocketClient.ConnectAsync(address);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await server.BroadcastTextAsync("line-ready");
        TestAssert.Equal("line-ready", (await first.ReceiveAsync()).Text);
        TestAssert.Equal("line-ready", (await second.ReceiveAsync()).Text);
        await server.StopAsync();
    }

    internal static async Task CloseWaitsForActualCloseAfterDataAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        var prefix = new Uri($"http://127.0.0.1:{port}/process/");
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix.AbsoluteUri);
        listener.Start();
        var dataSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("stray")),
                WebSocketMessageType.Text, true, CancellationToken.None);
            dataSent.TrySetResult(true);
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[32]), CancellationToken.None);
            TestAssert.Equal(WebSocketMessageType.Close, result.MessageType);
            closeSeen.TrySetResult(true);
            await allowReply.Task;
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        });
        try
        {
            using var client = await WebSocketClient.ConnectAsync(address);
            await dataSent.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task closing = client.CloseAsync();
            await closeSeen.Task.WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.True(!closing.IsCompleted);
            allowReply.TrySetResult(true);
            await closing.WaitAsync(TimeSpan.FromSeconds(2));
            await peer.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { allowReply.TrySetResult(true); listener.Close(); }
    }

    internal static async Task CloseWithPendingReceiveDoesNotStartAnotherReceiveAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/process/");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            entered.TrySetResult(true);
            await peer.ReceiveAsync(token);
        });
        await server.StartAsync();
        using var client = await WebSocketClient.ConnectAsync(address);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<WebSocketMessage> receiving = client.ReceiveAsync();
        var receiveGate = (SemaphoreSlim)typeof(WebSocketConnection).GetField("receiveGate",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
        TestAssert.True(SpinWait.SpinUntil(() => receiveGate.CurrentCount == 0, TimeSpan.FromSeconds(2)),
            "첫 수신이 시작되지 않았습니다.");
        await client.CloseAsync().WaitAsync(TimeSpan.FromSeconds(3));
        TestAssert.True(await receiving.WaitAsync(TimeSpan.FromSeconds(2)) == null);
        await server.StopAsync();
    }

    internal static async Task CloseTimeoutAbortsUnresponsivePeerAsync()
    {
        int port = FreePort();
        var address = new Uri($"ws://127.0.0.1:{port}/process/");
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/process/");
        listener.Start();
        var closeSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[8]), CancellationToken.None);
            TestAssert.Equal(WebSocketMessageType.Close, result.MessageType);
            closeSeen.TrySetResult(true);
            await release.Task;
        });
        try
        {
            using var client = await WebSocketClient.ConnectAsync(address);
            Task closing = client.CloseAsync();
            await closeSeen.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await TestAssert.ThrowsAsync<CommunicationException>(() => closing.WaitAsync(TimeSpan.FromSeconds(3)));
            TestAssert.True(!client.IsOpen);
        }
        finally
        {
            release.TrySetResult(true);
            listener.Close();
            try { await peer.WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
        }
    }

    internal static async Task OldBroadcastCannotReachRestartedRunAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/process/");
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new WebSocketServer(address, _ => true, async (peer, token) =>
        {
            entered.TrySetResult(true);
            try { await peer.ReceiveAsync(token); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (WebSocketException) { }
        });
        await server.StartAsync();
        var field = typeof(WebSocketServer).GetField("broadcastGate", BindingFlags.NonPublic | BindingFlags.Instance);
        var gate = (SemaphoreSlim)field.GetValue(server);
        await gate.WaitAsync();
        bool held = true;
        try
        {
            Task old = server.BroadcastTextAsync("old-run");
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            await server.StartAsync();
            using var client = await WebSocketClient.ConnectAsync(address);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Task next = server.BroadcastTextAsync("new-run");
            gate.Release();
            held = false;
            try { await old; throw new Exception("이전 실행의 브로드캐스트가 성공했습니다."); }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
            await next.WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.Equal("new-run", (await client.ReceiveAsync()).Text);
            await server.StopAsync();
        }
        finally { if (held) gate.Release(); }
    }

    internal static async Task StopWaitsForInProgressSignalAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/process/");
        using var server = new WebSocketServer(address, _ => true, (_, __) => Task.CompletedTask);
        await server.StartAsync();
        object run = typeof(WebSocketServer).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(server);
        var cancellation = (CancellationTokenSource)run.GetType().GetField("Cancellation",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(run);
        var listener = (HttpListener)run.GetType().GetField("Listener",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(run);
        var signaling = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() =>
        {
            signaling.TrySetResult(true);
            release.Task.GetAwaiter().GetResult();
        });
        Task first = Task.Run(() => server.StopAsync());
        try
        {
            await signaling.Task.WaitAsync(TimeSpan.FromSeconds(2));
            listener.Stop();
            Task second = server.StopAsync();
            server.Dispose();
            TestAssert.True(await Task.WhenAny(server.Completion, Task.Delay(100)) != server.Completion);
            TestAssert.True(!second.IsCompleted);
            release.TrySetResult(true);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally { release.TrySetResult(true); }
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }
}
