using FieldLink.Communication.Diagnostics;
using FieldLink.Samples;

namespace FieldLink.Communication.Tests;

internal static class DeviceLifecycleTests
{
    internal static async Task UdpDeviceRejectionKeepsSocketAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        using IUdpClient client = new FieldLink.Communication.Udp.UdpClient(
            (System.Net.IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        Task<byte[]> rejected = client.ExchangeAsync([0x20]);
        var first = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 0x31, 0 }, first.RemoteEndPoint);
        DeviceCommandException error = await TestAssert.ThrowsAsync<DeviceCommandException>(async () =>
        {
            byte[] response = await rejected;
            if (response[0] != 0)
                throw new DeviceCommandException(response[0]);
        });
        TestAssert.Equal((byte)0x31, error.ErrorCode);
        TestAssert.Equal(ClientState.Open, client.State);
        TestAssert.True(client.LastStateChange.Error == null);
        Task<byte[]> next = client.ExchangeAsync([0x20]);
        var second = await peer.ReceiveAsync();
        TestAssert.Equal(first.RemoteEndPoint, second.RemoteEndPoint);
        await peer.SendAsync(new byte[] { 0, 42 }, second.RemoteEndPoint);
        TestAssert.Bytes([0, 42], await next);
    }

    internal static async Task UnexpectedCallbackExceptionStillFaultsConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var cause = new InvalidOperationException("Unexpected driver bug.");
        Exception actual = await TestAssert.ThrowsAsync<InvalidOperationException>(() =>
            pair.Client.ExecuteTransactionAsync<int>(_ => throw cause));
        TestAssert.True(ReferenceEquals(cause, actual));
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        TestAssert.True(ReferenceEquals(cause, pair.Client.LastStateChange.Error));
    }

    internal static async Task DisconnectTimeoutStillClosesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        await InitializeAndReadAsync(pair, device);
        Task closing = device.DisconnectAsync();
        TestAssert.Bytes([0x30, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TestAssert.FailureAsync(() => closing, CommunicationFailure.Timeout);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        TestAssert.True(!pair.Listener.Pending());
    }

    internal static async Task ApplicationRetriesConnectionWithoutSendingCommandsAsync()
    {
        using var reservation = new TestTcpListener(System.Net.IPAddress.Loopback, 0);
        reservation.Start();
        var endpoint = (System.Net.IPEndPoint)reservation.LocalEndpoint;
        reservation.Stop();
        using var listener = new TestTcpListener(endpoint);
        using ITcpClient client = new FieldLink.Communication.Tcp.TcpClient(endpoint,
            new FieldLink.Communication.Framing.FixedLengthFrame(2));
        var device = new SessionDevice(client);
        var failed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.StateChanged += (_, change) =>
        {
            if (change.State == ClientState.Faulted)
            {
                listener.Start();
                failed.TrySetResult(true);
            }
        };
        await ReconnectionExample.ReconnectAsync(device, 5, TimeSpan.FromMilliseconds(100));
        await failed.Task;
        using var peer = await listener.AcceptSocketAsync();
        TestAssert.Equal(ClientState.Open, client.State);
        TestAssert.Equal(1L, client.ConnectionGeneration);
        TestAssert.Equal(0, peer.Available);
        Task<byte> read = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [0, 9]);
        TestAssert.Bytes([0x20, 9], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [0, 44]);
        TestAssert.Equal((byte)44, await read);
    }

    internal static async Task ApplicationReconnectHonorsAttemptLimitAsync()
    {
        using var listener = new TestTcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        using ITcpClient client = new FieldLink.Communication.Tcp.TcpClient(endpoint,
            new FieldLink.Communication.Framing.FixedLengthFrame(2));
        var device = new SessionDevice(client);
        int failures = 0;
        var notified = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.StateChanged += (_, change) =>
        {
            if (change.State == ClientState.Faulted && Interlocked.Increment(ref failures) == 3)
                notified.TrySetResult(true);
        };
        await TestAssert.FailureAsync(() => ReconnectionExample.ReconnectAsync(device, 3, TimeSpan.Zero),
            CommunicationFailure.SocketError);
        await notified.Task;
        TestAssert.Equal(3, failures);
        TestAssert.Equal(ClientState.Faulted, client.State);
        TestAssert.Equal(0L, client.ConnectionGeneration);
    }

    internal static async Task ApplicationCanCancelReconnectDelayAsync()
    {
        using var listener = new TestTcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        using ITcpClient client = new FieldLink.Communication.Tcp.TcpClient(endpoint,
            new FieldLink.Communication.Framing.FixedLengthFrame(2));
        var device = new SessionDevice(client);
        using var cancellation = new CancellationTokenSource();
        var failed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.StateChanged += (_, change) =>
        {
            if (change.State == ClientState.Faulted)
                failed.TrySetResult(true);
        };
        Task reconnect = ReconnectionExample.ReconnectAsync(device, 3, TimeSpan.FromSeconds(30), cancellation.Token);
        await failed.Task;
        cancellation.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => reconnect);
        TestAssert.Equal(ClientState.Faulted, client.State);
        TestAssert.Equal(0L, client.ConnectionGeneration);
    }

    internal static async Task DisconnectRejectsQueuedAndNewCommandsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task<byte> active = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        Task queued = device.WriteAsync(99);
        Task closing = device.DisconnectAsync();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => device.ReadAsync());
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => device.OpenAsync());
        await TcpFixture.WriteAsync(pair.Peer, [0, 42]);
        TestAssert.Equal((byte)42, await active);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => queued);
        TestAssert.Bytes([0x30, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [0, 0]);
        await closing;
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        TestAssert.True(pair.Client.LastStateChange.Error == null);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => device.ReadAsync());
        TestAssert.Equal(0, await pair.Peer.ReceiveAsync(new byte[1], System.Net.Sockets.SocketFlags.None));

        await device.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        Task<byte> reopened = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [0, 8]);
        TestAssert.Bytes([0x20, 8], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [0, 43]);
        TestAssert.Equal((byte)43, await reopened);
    }

    internal static async Task DisconnectRejectionStillClosesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        await InitializeAndReadAsync(pair, device);
        Task closing = device.DisconnectAsync();
        TestAssert.Bytes([0x30, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [0x51, 0]);
        DeviceCommandException error = await TestAssert.ThrowsAsync<DeviceCommandException>(() => closing);
        TestAssert.Equal((byte)0x51, error.ErrorCode);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        await device.DisconnectAsync();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => device.ReadAsync());
    }

    internal static async Task DisconnectCancellationStillClosesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        await InitializeAndReadAsync(pair, device);
        using var cancellation = new CancellationTokenSource();
        Task closing = device.DisconnectAsync(cancellation.Token);
        TestAssert.Bytes([0x30, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => closing, CommunicationFailure.Cancelled);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
    }

    internal static async Task DisconnectCancellationAbortsActiveRequestAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task<byte> active = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        using var cancellation = new CancellationTokenSource();
        Task closing = device.DisconnectAsync(cancellation.Token);
        cancellation.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => closing);
        await TestAssert.ThrowsAsync<CommunicationException>(() => active);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
    }

    internal static async Task WriteRejectionKeepsSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task first = device.WriteAsync(99);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x21, 7, 99], await TcpFixture.ReadExactlyAsync(pair.Peer, 3));
        await TcpFixture.WriteAsync(pair.Peer, [0x32, 0]);
        DeviceCommandException error = await TestAssert.ThrowsAsync<DeviceCommandException>(() => first);
        TestAssert.Equal((byte)0x32, error.ErrorCode);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        Task next = device.WriteAsync(100);
        TestAssert.Bytes([0x21, 7, 100], await TcpFixture.ReadExactlyAsync(pair.Peer, 3));
        await TcpFixture.WriteAsync(pair.Peer, [0, 0]);
        await next;
        TestAssert.Equal(1L, pair.Client.ConnectionGeneration);
    }

    internal static async Task ManualReconnectDoesNotReplayFailedWriteAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task write = device.WriteAsync(99);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x21, 7, 99], await TcpFixture.ReadExactlyAsync(pair.Peer, 3));
        pair.Peer.Dispose();
        await TestAssert.ThrowsAsync<CommunicationException>(() => write);
        TestAssert.True(!pair.Listener.Pending(), "No reconnect until the application requests it");
        await device.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal(0, peer.Available);
        Task<byte> read = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [0, 8]);
        TestAssert.Bytes([0x20, 8], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [0, 43]);
        TestAssert.Equal((byte)43, await read);
        TestAssert.Equal(2L, pair.Client.ConnectionGeneration);
    }

    private static async Task InitializeAndReadAsync(TcpFixture pair, SessionDevice device)
    {
        Task<byte> read = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [0, 42]);
        TestAssert.Equal((byte)42, await read);
    }

    internal static async Task DeviceRejectionKeepsConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task<byte> rejected = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0, 7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [0x31, 0]);
        DeviceCommandException error = await TestAssert.ThrowsAsync<DeviceCommandException>(() => rejected);
        TestAssert.Equal((byte)0x31, error.ErrorCode);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        TestAssert.Equal(1L, pair.Client.ConnectionGeneration);

        Task<byte> next = device.ReadAsync();
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [0, 42]);
        TestAssert.Equal((byte)42, await next);
        TestAssert.True(!pair.Listener.Pending());
    }

    internal static async Task InitializationRejectionClosesConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var device = new SessionDevice(pair.Client);
        Task<byte> rejected = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [0x41, 0]);
        DeviceCommandException error = await TestAssert.ThrowsAsync<DeviceCommandException>(() => rejected);
        TestAssert.Equal((byte)0x41, error.ErrorCode);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => device.ReadAsync());
        TestAssert.True(!pair.Listener.Pending());

        await device.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        Task<byte> next = device.ReadAsync();
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [0, 8]);
        TestAssert.Bytes([0x20, 8], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [0, 43]);
        TestAssert.Equal((byte)43, await next);
        TestAssert.Equal(2L, pair.Client.ConnectionGeneration);
    }
}
