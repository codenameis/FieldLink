using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Robot.ABB.Clients;
using FieldLink.Robot.ABB.Protocols;
using FieldLink.Robot.ABB.Servers;
using FieldLink.Robot.FANUC.Clients;
using FieldLink.Robot.FANUC.Servers;
using FieldLink.Robot.Hyundai.Protocols;
using FieldLink.Robot.Hyundai.Servers;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task TcpServerStopSurvivesThrowingCancellationCallbackAsync()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new FieldLink.Communication.Tcp.TcpServer(new IPEndPoint(IPAddress.Loopback, 0), async (connection, token) =>
        {
            using var registration = token.Register(() => throw new InvalidOperationException("구독자 오류"));
            entered.TrySetResult(true);
            await connection.ReceiveAsync(new FixedLengthFrame(1));
        });
        await server.StartAsync();
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(server.LocalEndPoint); await entered.Task;
        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        TestAssert.True(!server.IsRunning);
        TestAssert.True(server.LastConnectionError is AggregateException);
    }
    internal static async Task FanucServerWordBitAndSnapshotRoundTripAsync()
    {
        using var server = new FanucTcpServer(new IPEndPoint(IPAddress.Loopback, 0));
        await server.StartAsync();
        using var transport = new FieldLink.Communication.Tcp.TcpClient(server.LocalEndPoint, new FixedLengthFrame(1));
        var robot = new FanucTcpClient(transport);
        await transport.OpenAsync();
        TestAssert.True((await robot.WriteAsync("D100", new byte[] { 0x34, 0x12, 0xCD, 0xAB })).IsSuccess);
        TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], server.Read("D100", 2).Content);
        TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], (await robot.ReadAsync("D100", 2)).Content);
        TestAssert.True((await robot.WriteAsync("M1", new[] { true, false, true })).IsSuccess);
        TestAssert.True((await robot.ReadBoolAsync("M1", 3)).Content.SequenceEqual(new[] { true, false, true }));
        TestAssert.True((await robot.WriteAsync("GI1", new ushort[] { 123, 456 })).IsSuccess);
        TestAssert.True((await robot.ReadUInt16Async("GI1", 2)).Content.SequenceEqual(new ushort[] { 123, 456 }));
        byte[] snapshot = server.SaveSnapshot();
        server.Write("D100", new byte[] { 1, 0 }); server.LoadSnapshot(snapshot);
        TestAssert.Bytes([0x34, 0x12], server.Read("D100", 1).Content);
        TestAssert.True(!server.Read("D0", 1).IsSuccess);
        server.EnableWrite = false;
        await TestAssert.ThrowsAsync<FieldLink.Communication.Diagnostics.CommunicationException>(() => robot.WriteAsync("D100", new byte[] { 0, 0 }));
        TestAssert.Bytes([0x34, 0x12], server.Read("D100", 1).Content);
        await server.StopAsync();
        TestAssert.True(!server.IsRunning);
    }

    internal static async Task TcpServerPartialFramesStopAndRestartAsync()
    {
        ITcpServerConnection? saved = null;
        var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new FieldLink.Communication.Tcp.TcpServer(new IPEndPoint(IPAddress.Loopback, 0), async (connection, token) =>
        {
            saved = connection;
            for (int i = 0; i < 2; i++) await connection.SendAsync(await connection.ReceiveAsync(new FixedLengthFrame(2)));
            waiting.TrySetResult(true);
            await connection.ReceiveAsync(new FixedLengthFrame(2));
        });
        for (int run = 0; run < 2; run++)
        {
            await server.StartAsync();
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(server.LocalEndPoint);
            await TcpFixture.WriteAsync(socket, [1]);
            await TcpFixture.WriteAsync(socket, [2, 3, 4]);
            TestAssert.Bytes([1, 2, 3, 4], await TcpFixture.ReadExactlyAsync(socket, 4));
            await waiting.Task;
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.True(!server.IsRunning);
            await TestAssert.ThrowsAsync<ObjectDisposedException>(() => saved!.SendAsync([1]));
            waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal static async Task HyundaiServerTracksPeersAndClearsThemOnRestartAsync()
    {
        using var server = new HyundaiUdpServer(new IPEndPoint(IPAddress.Loopback, 0));
        using var robot = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await server.StartAsync();
        byte[] start = new byte[64]; start[0] = (byte)'S';
        await robot.SendAsync(start, server.LocalEndPoint);
        byte[] reply = (await robot.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer;
        TestAssert.Equal((byte)'S', reply[0]); TestAssert.Equal(1, BitConverter.ToInt32(reply, 4));
        var received = new TaskCompletionSource<HyundaiPositionCorrection>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.PositionReceived += data => received.TrySetResult(data);
        byte[] position = new byte[64]; position[0] = (byte)'P'; BitConverter.GetBytes(0.123).CopyTo(position, 16);
        await robot.SendAsync(position, server.LocalEndPoint);
        TestAssert.Equal(123d, (await received.Task.WaitAsync(TimeSpan.FromSeconds(2))).Data[0]);
        TestAssert.True((await server.MoveXAsync(10)).IsSuccess);
        var increment = (await robot.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2))).Buffer;
        TestAssert.Equal(0.01, BitConverter.ToDouble(increment, 16));
        TestAssert.Equal(2, BitConverter.ToInt32(increment, 4));
        await server.StopAsync(); await server.StartAsync();
        TestAssert.True(server.LastPosition == null);
        TestAssert.True(!(await server.MoveXAsync(10)).IsSuccess);
        await server.StopAsync();
    }

    internal static async Task AbbServerAuthenticatesAndServesClientRequestsAsync()
    {
        var probe = new TestTcpListener(IPAddress.Loopback, 0); probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var address = new Uri($"http://127.0.0.1:{port}/");
        using var server = new AbbHttpServer(address); await server.StartAsync();
        using var http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(3) };
        using (var missing = await http.GetAsync("rw/panel/ctrlstate")) TestAssert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Basic malformed!");
        using (var malformed = await http.GetAsync("rw/panel/ctrlstate")) TestAssert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        http.DefaultRequestHeaders.Remove("Authorization");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("Default User:robotics")));
        var client = new AbbHttpClient(http, address);
        var state = await client.ExecuteAsync(AbbRequestBuilder.GetCtrlState());
        TestAssert.True(state.IsSuccess, state.Message); TestAssert.Equal("motoron", state.Content);
        using (var missing = await http.GetAsync("unregistered")) TestAssert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await server.StopAsync(); await server.StartAsync();
        TestAssert.True((await client.ExecuteAsync(AbbRequestBuilder.GetCtrlState())).IsSuccess);
        await server.StopAsync();
    }
}
