using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using UdpClient = FieldLink.Communication.Udp.UdpClient;

namespace FieldLink.Communication.Tests;

internal static class DeviceClientTests
{
    internal static async Task TcpContractReusesConnectionAndDefaultFrameAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using ITcpClient tcp = new TcpClient((IPEndPoint)listener.LocalEndpoint, new FixedLengthFrame(2));
        ICommunicationClient client = tcp;
        await client.OpenAsync(TcpFixture.Timeout);
        using Socket peer = await listener.AcceptSocketAsync();
        Task<byte[]> response = client.ExchangeAsync([1], TcpFixture.Timeout);
        TestAssert.Bytes([1], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [2, 3]);
        TestAssert.Bytes([2, 3], await response);
        await client.SendAsync([4], TcpFixture.Timeout);
        TestAssert.Bytes([4], await TcpFixture.ReadExactlyAsync(peer, 1));
        TestAssert.Equal(1L, tcp.ConnectionGeneration);
        TestAssert.True(!listener.Pending(), "Communication must reuse the explicitly opened connection");
    }

    internal static async Task UdpContractReusesSocketAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using ICommunicationClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync(TcpFixture.Timeout);
        await client.SendAsync([1], TcpFixture.Timeout);
        UdpReceiveResult first = await peer.ReceiveAsync();
        Task<byte[]> response = client.ExchangeAsync([2], TcpFixture.Timeout);
        UdpReceiveResult second = await peer.ReceiveAsync();
        TestAssert.Equal(first.RemoteEndPoint, second.RemoteEndPoint);
        TestAssert.Bytes([2], second.Buffer);
        await peer.SendAsync(new byte[] { 3 }, second.RemoteEndPoint);
        TestAssert.Bytes([3], await response);
    }

    internal static async Task TcpReadAfterCloseDoesNotReconnectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        long generation = pair.Client.ConnectionGeneration;
        pair.Client.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.ExchangeAsync([1]));
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.SendAsync([2]));
        TestAssert.Equal(generation, pair.Client.ConnectionGeneration);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        TestAssert.True(!pair.Listener.Pending(), "Read and Send must not silently reconnect");
    }

    internal static async Task UdpReadAfterCloseDoesNotReopenAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        client.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeAsync([1]));
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([2]));
        TestAssert.Equal(ClientState.Closed, client.State);
        TestAssert.Equal(0, peer.Available);
    }

    internal static async Task TcpFailureRequiresExplicitReopenAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> failed = pair.Client.ExchangeAsync([1], TimeSpan.FromMilliseconds(100));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TestAssert.FailureAsync(() => failed, CommunicationFailure.Timeout);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.ExchangeAsync([2]));
        TestAssert.Equal(1L, pair.Client.ConnectionGeneration);
        TestAssert.True(!pair.Listener.Pending());

        await pair.Client.OpenAsync();
        using Socket peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal(2L, pair.Client.ConnectionGeneration);
        Task<byte[]> response = pair.Client.ExchangeAsync([3], TcpFixture.Timeout);
        TestAssert.Bytes([3], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [4]);
        TestAssert.Bytes([4], await response);
    }

    internal static async Task TransactionExposesConnectionAndCancellationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var entered = new TaskCompletionSource<ITcpTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> active = pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            entered.SetResult(transaction);
            try { await Task.Delay(Timeout.Infinite, transaction.CancellationToken); }
            finally { exited.SetResult(true); }
            return true;
        });
        ITcpTransaction transaction = await entered.Task;
        TestAssert.Equal(pair.Client.ConnectionGeneration, transaction.ConnectionGeneration);
        pair.Client.Close();
        await TestAssert.FailureAsync(() => active, CommunicationFailure.ConnectionClosed);
        await exited.Task.WaitAsync(TimeSpan.FromSeconds(1));
        TestAssert.True(transaction.CancellationToken.IsCancellationRequested);
    }

    internal static async Task DriverInitializesOncePerConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var driver = new RegisterDevice(pair.Client);
        Task<byte> first = driver.ReadAsync();
        Task<byte> second = driver.ReadAsync();
        await ReplyToSessionAsync(pair.Peer, 7, 2);
        TestAssert.Equal((byte)42, await first);
        TestAssert.Equal((byte)42, await second);

        pair.Client.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => driver.ReadAsync());
        TestAssert.True(!pair.Listener.Pending());
        await pair.Client.OpenAsync();
        Task<byte> afterReconnect = driver.ReadAsync();
        using Socket peer = await pair.Listener.AcceptSocketAsync();
        await ReplyToSessionAsync(peer, 8, 1);
        TestAssert.Equal((byte)42, await afterReconnect);
        TestAssert.Equal(2L, pair.Client.ConnectionGeneration);
    }

    private static async Task ReplyToSessionAsync(Socket peer, byte session, int reads)
    {
        // 테스트 전용 프로토콜: 0x10은 세션 요청, 0x20은 세션을 사용한 값 읽기다.
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [session]);
        for (int index = 0; index < reads; index++)
        {
            TestAssert.Bytes([0x20, session], await TcpFixture.ReadExactlyAsync(peer, 2));
            await TcpFixture.WriteAsync(peer, [42]);
        }
    }

    private sealed class RegisterDevice
    {
        private readonly ITcpClient client;
        private long initializedGeneration;
        private byte session;

        internal RegisterDevice(ITcpClient client) { this.client = client; }

        internal Task<byte> ReadAsync()
        {
            return client.ExecuteTransactionAsync(async transaction =>
            {
                if (initializedGeneration != transaction.ConnectionGeneration)
                {
                    byte[] reply = await transaction.ExchangeAsync([0x10]);
                    session = reply[0];
                    initializedGeneration = transaction.ConnectionGeneration;
                }
                byte[] value = await transaction.ExchangeAsync([0x20, session]);
                return value[0];
            });
        }
    }
}
