using FieldLink.Communication.Diagnostics;
using SocketUdpClient = System.Net.Sockets.UdpClient;
using FieldLink.Communication;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using UdpClient = FieldLink.Communication.Udp.UdpClient;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tests;

internal static class ClientTests
{
    private static readonly IFrameBoundary OneByte = new FixedLengthFrame(1);

    internal static async Task SharedContractTcpAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using ICommunicationClient transport = new TcpClient((IPEndPoint)listener.LocalEndpoint, OneByte);
        await transport.OpenAsync();
        using Socket peer = await listener.AcceptSocketAsync();
        Task<byte[]> reply = transport.ExchangeAsync([7]);
        TestAssert.Bytes([7], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [8]);
        TestAssert.Bytes([8], await reply);
        TestAssert.Equal(ClientState.Open, transport.State);
        transport.Close();
        TestAssert.Equal(ClientState.Closed, transport.State);
    }

    internal static async Task SharedContractUdpAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using ICommunicationClient transport = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await transport.OpenAsync();
        Task<byte[]> reply = transport.ExchangeAsync([7]);
        UdpReceiveResult request = await peer.ReceiveAsync();
        TestAssert.Bytes([7], request.Buffer);
        await peer.SendAsync(new byte[] { 8 }, request.RemoteEndPoint);
        TestAssert.Bytes([8], await reply);
        transport.Close();
        TestAssert.Equal(ClientState.Closed, transport.State);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync([9]));
    }

    internal static async Task OrderedStateNotificationsAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        var changes = new ConcurrentQueue<ClientStateChangedEventArgs>();
        Task<ClientStateChangedEventArgs> disposed = NextState(client, ClientState.Disposed);
        client.StateChanged += (_, change) => changes.Enqueue(change);
        await client.OpenAsync();
        client.Close();
        client.Close();
        client.Dispose();
        client.Close();
        await disposed;
        await UntilAsync(() => changes.Count == 4);
        var expected = new[] { ClientState.Opening, ClientState.Open, ClientState.Closed, ClientState.Disposed };
        var actual = changes.ToArray();
        for (int index = 0; index < expected.Length; index++)
        {
            TestAssert.Equal(expected[index], actual[index].State);
            TestAssert.Equal(index + 1L, actual[index].Sequence);
            TestAssert.True(actual[index].Error == null);
        }
        TestAssert.Equal(ClientState.Disposed, client.State);
        TestAssert.Equal(4L, client.LastStateChange.Sequence);
    }

    internal static async Task IdlePeerCloseNotificationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<ClientStateChangedEventArgs> closed = NextState(pair.Client, ClientState.Closed);
        pair.Peer.Shutdown(SocketShutdown.Send);
        ClientStateChangedEventArgs change = await closed;
        TestAssert.True(change.Error is CommunicationException);
        var error = (CommunicationException)change.Error!;
        TestAssert.Equal(CommunicationFailure.ConnectionClosed, error.Failure);
        TestAssert.Equal(CommunicationStage.Monitoring, error.Stage);
        TestAssert.True(!pair.Client.IsConnected);
        TestAssert.True(ReferenceEquals(change, pair.Client.LastStateChange));
    }

    internal static async Task IdlePeerResetNotificationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var stopped = new TaskCompletionSource<ClientStateChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        pair.Client.StateChanged += (_, change) =>
        {
            if (change.State == ClientState.Closed || change.State == ClientState.Faulted)
                stopped.TrySetResult(change);
        };
        pair.Peer.LingerState = new LingerOption(true, 0);
        pair.Peer.Dispose();
        var change = await stopped.Task.WaitAsync(TcpFixture.Timeout);
        TestAssert.True(change.Error is CommunicationException);
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task MonitorPreservesPendingAndBufferedDataAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [11, 12]);
        pair.Peer.Shutdown(SocketShutdown.Send);
        await Task.Delay(600);
        TestAssert.True(pair.Client.IsConnected, "Monitoring must not discard data queued before FIN.");
        byte[] first = await pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync());
        TestAssert.Bytes([11], first);
        await Task.Delay(600);
        TestAssert.True(pair.Client.IsConnected, "Monitoring must not discard a buffered following frame.");
        Task<ClientStateChangedEventArgs> closed = NextState(pair.Client, ClientState.Closed);
        byte[] second = await pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync());
        TestAssert.Bytes([12], second);
        await closed;
    }

    internal static async Task MonitorCanBeDisabledAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient((IPEndPoint)listener.LocalEndpoint, OneByte,
            disconnectCheckInterval: Timeout.InfiniteTimeSpan);
        await client.OpenAsync();
        using Socket peer = await listener.AcceptSocketAsync();
        peer.Shutdown(SocketShutdown.Send);
        await Task.Delay(600);
        TestAssert.True(client.IsConnected);
        await TestAssert.FailureAsync(() => client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync()),
            CommunicationFailure.ConnectionClosed);
    }

    internal static async Task FailureNotificationKeepsDiagnosticsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<ClientStateChangedEventArgs> failed = NextState(pair.Client, ClientState.Faulted);
        Task<byte[]> request = pair.Client.ExchangeAsync([1], OneByte, TimeSpan.FromMilliseconds(150));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        CommunicationException error = await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        var change = await failed;
        TestAssert.True(ReferenceEquals(error, change.Error), "Task and notification must preserve the same diagnostic exception.");
        TestAssert.Equal(1L, error.BytesSent);
        TestAssert.Equal(CommunicationStage.Receiving, error.Stage);
    }

    internal static async Task LocalCloseDoesNotBecomeFaultedAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        pair.Client.Close();
        await TestAssert.ThrowsAsync<CommunicationException>(() => active);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        TestAssert.True(pair.Client.LastStateChange.Error == null);
    }

    internal static async Task ThrowingSubscriberDoesNotBreakCommunicationAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        var observerError = new InvalidOperationException("Observer failure");
        client.StateChanged += (_, _) => throw observerError;
        Task<ClientStateChangedEventArgs> opened = NextState(client, ClientState.Open);
        await client.OpenAsync();
        await opened;
        TestAssert.True(ReferenceEquals(observerError, client.LastNotificationError));
        await client.SendAsync([1]);
        TestAssert.Bytes([1], (await peer.ReceiveAsync()).Buffer);
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task SubscriberCanCloseAndReopenAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient((IPEndPoint)listener.LocalEndpoint, OneByte);
        var reopened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int openNotifications = 0;
        client.StateChanged += (_, change) =>
        {
            if (change.State != ClientState.Open || ++openNotifications != 1)
                return;
            try
            {
                client.Close();
                // 의도적으로 동기 대기하여 통신 잠금 안에서 알림을 실행하지 않는지 검증한다.
                client.OpenAsync().GetAwaiter().GetResult();
                reopened.TrySetResult(true);
            }
            catch (Exception error) { reopened.TrySetException(error); }
        };
        await client.OpenAsync();
        using Socket first = await listener.AcceptSocketAsync();
        using Socket second = await listener.AcceptSocketAsync();
        await reopened.Task.WaitAsync(TcpFixture.Timeout);
        TestAssert.Equal(2L, client.ConnectionGeneration);
        await client.SendAsync([9]);
        TestAssert.Bytes([9], await TcpFixture.ReadExactlyAsync(second, 1));
    }

    internal static async Task QueuedRequestDoesNotCrossReconnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> first = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        Task<byte[]> queued = pair.Client.ExchangeAsync([2]);
        pair.Client.Close();
        Task reopen = pair.Client.OpenAsync();
        await TestAssert.ThrowsAsync<CommunicationException>(() => first);
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.ConnectionClosed);
        await reopen;
        using Socket peer = await pair.Listener.AcceptSocketAsync();
        await pair.Client.SendAsync([3]);
        TestAssert.Bytes([3], await TcpFixture.ReadExactlyAsync(peer, 1));
        TestAssert.True(!peer.Poll(0, SelectMode.SelectRead), "Old queued command must not enter the new connection.");
    }

    internal static async Task QueuedCancellationDoesNotPublishDisconnectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        long sequence = pair.Client.LastStateChange.Sequence;
        Task<byte[]> active = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> queued = pair.Client.ExchangeAsync([2], cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.Cancelled);
        TestAssert.Equal(sequence, pair.Client.LastStateChange.Sequence);
        await TcpFixture.WriteAsync(pair.Peer, [1]);
        await active;
    }

    internal static async Task RequestBufferCanBeReusedAfterCompletionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        byte[] data = [2];
        Task<byte[]> queued = pair.Client.ExchangeAsync(data);
        await TcpFixture.WriteAsync(pair.Peer, [1]);
        await active;
        TestAssert.Bytes([2], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [3]);
        TestAssert.Bytes([3], await queued);
        // 버퍼는 이 배열을 사용하는 Task가 끝난 뒤 재사용한다.
        data[0] = 99;
        Task<byte[]> reused = pair.Client.ExchangeAsync(data);
        TestAssert.Bytes([99], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [4]);
        TestAssert.Bytes([4], await reused);
    }

    internal static async Task UdpDefaultTimeoutRequiresExplicitReopenAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using ICommunicationClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!, timeout: TimeSpan.FromMilliseconds(150));
        await client.OpenAsync();
        Task<ClientStateChangedEventArgs> faulted = NextState(client, ClientState.Faulted);
        Task<byte[]> request = client.ExchangeAsync([1]);
        await peer.ReceiveAsync();
        var error = await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        TestAssert.True(ReferenceEquals(error, (await faulted).Error));
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([2]));
        await client.OpenAsync();
        await client.SendAsync([3]);
        TestAssert.Bytes([3], (await peer.ReceiveAsync()).Buffer);
    }

    internal static async Task UdpBufferDoesNotMutateReturnedResponseAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        Task<byte[]> firstTask = client.ExchangeAsync([1]);
        var firstRequest = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 11, 12 }, firstRequest.RemoteEndPoint);
        byte[] first = await firstTask;
        Task<byte[]> secondTask = client.ExchangeAsync([2]);
        var secondRequest = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 21, 22 }, secondRequest.RemoteEndPoint);
        TestAssert.Bytes([21, 22], await secondTask);
        TestAssert.Bytes([11, 12], first);
    }

    internal static async Task PreCancelledOpenLeavesStateClosedAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using ICommunicationClient tcp = new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), OneByte);
        using ICommunicationClient udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 1));
        foreach (ICommunicationClient client in new[] { tcp, udp })
        {
            await TestAssert.FailureAsync(() => client.OpenAsync(cancellation.Token), CommunicationFailure.Cancelled);
            TestAssert.Equal(ClientState.Closed, client.State);
            TestAssert.Equal(0L, client.LastStateChange.Sequence);
        }
    }

    private static Task<ClientStateChangedEventArgs> NextState(ICommunicationClient client, ClientState state)
    {
        var completion = new TaskCompletionSource<ClientStateChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ClientStateChangedEventArgs>? handler = null;
        handler = (_, change) =>
        {
            if (change.State != state)
                return;
            client.StateChanged -= handler;
            completion.TrySetResult(change);
        };
        client.StateChanged += handler;
        return completion.Task.WaitAsync(TcpFixture.Timeout);
    }

    internal static async Task UdpQueuedRequestDoesNotCrossReopenAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        Task<byte[]> active = client.ExchangeAsync([1]);
        await peer.ReceiveAsync();
        Task<byte[]> queued = client.ExchangeAsync([2]);
        client.Close();
        Task reopen = client.OpenAsync();
        await TestAssert.ThrowsAsync<CommunicationException>(() => active);
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.ConnectionClosed);
        await reopen;
        await client.SendAsync([3]);
        TestAssert.Bytes([3], (await peer.ReceiveAsync()).Buffer);
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task OpenFailuresPublishDiagnosticsAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint refusedAddress = (IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        using var occupied = new SocketUdpClient(AddressFamily.InterNetwork);
        occupied.Client.ExclusiveAddressUse = true;
        occupied.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        using ICommunicationClient tcp = new TcpClient(refusedAddress, OneByte);
        using ICommunicationClient udp = new UdpClient(refusedAddress, localEndPoint: (IPEndPoint)occupied.Client.LocalEndPoint!);
        foreach (ICommunicationClient client in new[] { tcp, udp })
        {
            Task<ClientStateChangedEventArgs> faulted = NextState(client, ClientState.Faulted);
            var error = await TestAssert.FailureAsync(() => client.OpenAsync(), CommunicationFailure.SocketError);
            TestAssert.True(ReferenceEquals(error, (await faulted).Error));
            TestAssert.Equal(CommunicationStage.Connecting, error.Stage);
            TestAssert.True(error.SocketErrorCode.HasValue);
        }
    }

    internal static async Task TcpConfiguredDefaultTimeoutAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using ICommunicationClient client = new TcpClient((IPEndPoint)listener.LocalEndpoint, OneByte,
            timeout: TimeSpan.FromMilliseconds(150));
        await client.OpenAsync();
        using Socket peer = await listener.AcceptSocketAsync();
        Task<byte[]> request = client.ExchangeAsync([1]);
        TestAssert.Bytes([1], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        TestAssert.Equal(ClientState.Faulted, client.State);
    }

    internal static async Task RepeatedOpenCloseAndDisposeAsync()
    {
        using var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient((IPEndPoint)listener.LocalEndpoint, OneByte,
            disconnectCheckInterval: TimeSpan.FromMilliseconds(1));
        for (int iteration = 0; iteration < 20; iteration++)
        {
            await client.OpenAsync();
            using Socket peer = await listener.AcceptSocketAsync();
            await client.SendAsync([(byte)iteration]);
            TestAssert.Bytes([(byte)iteration], await TcpFixture.ReadExactlyAsync(peer, 1));
            client.Close();
        }
        TestAssert.Equal(20L, client.ConnectionGeneration);
        client.Dispose();
        await Task.Delay(20);
        TestAssert.Equal(ClientState.Disposed, client.State);
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
    }

    internal static async Task SameDeviceDriverWorksOverTcpAndUdpAsync()
    {
        using var tcpPeer = new TestTcpListener(IPAddress.Loopback, 0);
        tcpPeer.Start();
        using ICommunicationClient tcp = new TcpClient((IPEndPoint)tcpPeer.LocalEndpoint, new FixedLengthFrame(4));
        using var udpPeer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using ICommunicationClient udp = new UdpClient((IPEndPoint)udpPeer.Client.LocalEndPoint!);
        await tcp.OpenAsync();
        using Socket tcpSocket = await tcpPeer.AcceptSocketAsync();
        await udp.OpenAsync();

        foreach (ICommunicationClient transport in new[] { tcp, udp })
        {
            var device = new TestRegisterClient(transport);
            Task<ushort> read = device.ReadAsync(42);
            if (ReferenceEquals(transport, tcp))
            {
                TestAssert.Bytes([1, 42], await TcpFixture.ReadExactlyAsync(tcpSocket, 2));
                await TcpFixture.WriteAsync(tcpSocket, [0, 0, 0, 9, 1, 0, 0x12, 0x34]);
            }
            else
            {
                var request = await udpPeer.ReceiveAsync();
                TestAssert.Bytes([1, 42], request.Buffer);
                await udpPeer.SendAsync(new byte[] { 0, 0, 0, 9 }, request.RemoteEndPoint);
                await udpPeer.SendAsync(new byte[] { 1, 0, 0x12, 0x34 }, request.RemoteEndPoint);
            }
            TestAssert.Equal((ushort)0x1234, await read);
        }
    }

    // 테스트 전용 가상 프로토콜이다. 실제 PLC 이식 완료를 의미하지 않는다.
    // 드라이버는 요청 ID·명령·응답을 관리하며 소켓이나 TCP 트랜잭션을 사용하지 않는다.
    private sealed class TestRegisterClient(ICommunicationClient transport)
    {
        private int nextRequestId;

        internal async Task<ushort> ReadAsync(byte address)
        {
            byte requestId = checked((byte)Interlocked.Increment(ref nextRequestId));
            byte[] response = await transport.ExchangeAsync([requestId, address], bytes =>
                bytes.Length != 4 ? ResponseDisposition.Reject :
                bytes[0] == requestId ? ResponseDisposition.Accept : ResponseDisposition.Ignore);
            if (response[1] != 0)
                throw new InvalidDataException("Device rejected the read command.");
            return (ushort)((response[2] << 8) | response[3]);
        }
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        while (!condition()) await Task.Delay(5, deadline.Token);
    }
}
