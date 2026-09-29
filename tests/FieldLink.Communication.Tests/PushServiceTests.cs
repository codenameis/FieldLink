using FieldLink.Communication.Push;
using System.Net;
using System.Net.WebSockets;

namespace FieldLink.Communication.Tests;

internal static class PushServiceTests
{
    internal static async Task TopicsAndAuthorizationAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/push/");
        using var server = new PushServer(address, request => request.Headers["X-Process-Key"] == "secret");
        await server.StartAsync();
        await TestAssert.ThrowsAsync<WebSocketException>(() => PushClient.ConnectAsync(address, "line.one"));
        Action<ClientWebSocketOptions> header = options => options.SetRequestHeader("X-Process-Key", "secret");
        using var first = await PushClient.ConnectAsync(address, "line.one", configure: header);
        using var second = await PushClient.ConnectAsync(address, "line.two", configure: header);
        await server.PublishAsync("line.one", "ready");
        TestAssert.Equal("ready", await first.ReceiveAsync());
        await server.PublishAsync("line.two", "running");
        TestAssert.Equal("running", await second.ReceiveAsync());
        await first.CloseAsync();
        await server.PublishAsync("line.one", "later");
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
    }

    internal static async Task ConcurrentPublishKeepsOrderAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/push/");
        using var server = new PushServer(address, _ => true);
        await server.StartAsync();
        using var first = await PushClient.ConnectAsync(address, "line");
        using var second = await PushClient.ConnectAsync(address, "line");
        Task[] publications = Enumerable.Range(0, 20).Select(i => server.PublishAsync("line", i.ToString())).ToArray();
        await Task.WhenAll(publications);
        var receivedFirst = new string[20];
        var receivedSecond = new string[20];
        for (int i = 0; i < 20; i++)
        {
            receivedFirst[i] = await first.ReceiveAsync();
            receivedSecond[i] = await second.ReceiveAsync();
        }
        TestAssert.True(receivedFirst.SequenceEqual(receivedSecond));
        TestAssert.True(receivedFirst.OrderBy(value => int.Parse(value)).SequenceEqual(Enumerable.Range(0, 20).Select(i => i.ToString())));
        await server.StopAsync();
    }

    internal static async Task InvalidTopicRejectedAsync()
    {
        var address = new Uri($"ws://127.0.0.1:{FreePort()}/push/");
        await TestAssert.ThrowsAsync<ArgumentException>(() => PushClient.ConnectAsync(address, "../../etc"));
        using var server = new PushServer(address, _ => true);
        await server.StartAsync();
        await TestAssert.ThrowsAsync<ArgumentException>(() => server.PublishAsync("bad/topic", "value"));
        await server.StopAsync();
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }
}
