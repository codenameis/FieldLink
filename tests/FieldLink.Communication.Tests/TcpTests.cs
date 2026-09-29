using FieldLink.Communication.Tcp;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tests;

internal static class TcpTests
{
    private static readonly IFrameBoundary OneByte = new FixedLengthFrame(1);

    internal static async Task LargeSendAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        byte[] payload = Enumerable.Range(0, 2 * 1024 * 1024).Select(index => (byte)index).ToArray();
        Task<byte[]> read = TcpFixture.ReadExactlyAsync(pair.Peer, payload.Length);
        await pair.Client.SendAsync(payload, TcpFixture.Timeout);
        TestAssert.Bytes(payload, await read);
    }

    internal static async Task ConcurrentRequestsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task server = EchoAsync(pair.Peer, 20);
        Task<byte[]>[] requests = Enumerable.Range(1, 20).Select(value =>
            pair.Client.ExchangeAsync([(byte)value], OneByte, TcpFixture.Timeout)).ToArray();
        byte[][] replies = await Task.WhenAll(requests);
        for (int index = 0; index < replies.Length; index++) TestAssert.Bytes([(byte)(index + 1)], replies[index]);
        await server;
    }

    private static async Task EchoAsync(Socket peer, int count)
    {
        for (int index = 0; index < count; index++)
            await TcpFixture.WriteAsync(peer, await TcpFixture.ReadExactlyAsync(peer, 1));
    }

    internal static async Task ExclusiveSequenceAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> sequence = pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            await transaction.ExchangeAsync([1], OneByte);
            return await transaction.ExchangeAsync([2], OneByte);
        }, TcpFixture.Timeout);
        Task<byte[]> queued = pair.Client.ExchangeAsync([3], OneByte, TcpFixture.Timeout);
        for (byte expected = 1; expected <= 3; expected++)
        {
            TestAssert.Bytes([expected], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
            await TcpFixture.WriteAsync(pair.Peer, [expected]);
        }
        TestAssert.Bytes([2], await sequence);
        TestAssert.Bytes([3], await queued);
    }

    internal static async Task PeerCloseDiagnosticsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> request = pair.Client.ExchangeAsync([7, 8], new FixedLengthFrame(4), TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 2);
        await TcpFixture.WriteAsync(pair.Peer, [9, 10]);
        pair.Peer.Shutdown(SocketShutdown.Send);
        var error = await TestAssert.FailureAsync(() => request, CommunicationFailure.ConnectionClosed);
        TestAssert.Equal(CommunicationStage.Receiving, error.Stage);
        TestAssert.Equal(2L, error.BytesSent);
        TestAssert.Equal(2L, error.BytesReceived);
        TestAssert.True(error.RemoteAddress.Contains("127.0.0.1"));
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task ReceiveTimeoutAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> request = pair.Client.ExchangeAsync([7], OneByte, TimeSpan.FromMilliseconds(200));
        TestAssert.Bytes([7], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        var failure = await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        TestAssert.Equal(1L, failure.BytesSent);
        TestAssert.True(!pair.Client.IsConnected);
        // 실패한 명령을 다시 보내지 않고 연결을 닫는다.
        try { TestAssert.Equal(0, await pair.Peer.ReceiveAsync(new byte[1], SocketFlags.None)); }
        catch (SocketException error) when (error.SocketErrorCode == SocketError.ConnectionReset) { /* Windows의 중단된 I/O 종료 */ }
    }

    internal static async Task ReceiveCancellationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> request = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout, cancellationToken: cancellation.Token);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => request, CommunicationFailure.Cancelled);
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task QueuedCancellationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> queued = pair.Client.ExchangeAsync([2], OneByte, TcpFixture.Timeout, cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var error = await TestAssert.FailureAsync(() => queued, CommunicationFailure.Cancelled);
        TestAssert.Equal(CommunicationStage.Waiting, error.Stage);
        TestAssert.Equal(0L, error.BytesSent);
        await TcpFixture.WriteAsync(pair.Peer, [1]);
        TestAssert.Bytes([1], await active);
        TestAssert.True(pair.Client.IsConnected);
    }

    internal static async Task QueuedTimeoutAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        var error = await TestAssert.FailureAsync(() => pair.Client.ExchangeAsync([2], OneByte, TimeSpan.FromMilliseconds(100)), CommunicationFailure.Timeout);
        TestAssert.Equal(CommunicationStage.Waiting, error.Stage);
        await TcpFixture.WriteAsync(pair.Peer, [1]);
        await active;
        TestAssert.True(pair.Client.IsConnected);
    }

    internal static async Task DisposeActiveAndQueuedAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        Task<byte[]> queued = pair.Client.ExchangeAsync([2], OneByte, TcpFixture.Timeout);
        pair.Client.Dispose();
        await TestAssert.FailureAsync(() => active, CommunicationFailure.Disposed);
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.Disposed);
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => pair.Client.OpenAsync(TcpFixture.Timeout));
    }

    internal static async Task ReconnectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        long generation = pair.Client.ConnectionGeneration;
        pair.Client.Close();
        TestAssert.True(!pair.Client.IsConnected);
        await pair.Client.OpenAsync(TcpFixture.Timeout);
        using Socket secondPeer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal(generation + 1, pair.Client.ConnectionGeneration);
        Task<byte[]> response = pair.Client.ExchangeAsync([3], OneByte, TcpFixture.Timeout);
        TestAssert.Bytes([3], await TcpFixture.ReadExactlyAsync(secondPeer, 1));
        await TcpFixture.WriteAsync(secondPeer, [4]);
        TestAssert.Bytes([4], await response);
    }

    internal static async Task IgnoreAndRejectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> response = pair.Client.ExchangeAsync([3], OneByte, TcpFixture.Timeout,
            bytes => bytes[0] == 3 ? ResponseDisposition.Accept : ResponseDisposition.Ignore);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [1, 2, 3]);
        TestAssert.Bytes([3], await response);
        Task<byte[]> rejected = pair.Client.ExchangeAsync([4], OneByte, TcpFixture.Timeout, _ => ResponseDisposition.Reject);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [9]);
        await TestAssert.FailureAsync(() => rejected, CommunicationFailure.ResponseRejected);
    }

    internal static async Task SingleDeadlineAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var watch = Stopwatch.StartNew();
        Task<byte[]> request = pair.Client.ExchangeAsync([1], new FixedLengthFrame(20), TimeSpan.FromMilliseconds(220));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [1]);
        await Task.Delay(100);
        await TcpFixture.WriteAsync(pair.Peer, [2]);
        await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        TestAssert.True(watch.Elapsed < TimeSpan.FromMilliseconds(900), "Receiving partial data must not reset deadline");
    }

    internal static async Task CallbackDeadlineAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> request = pair.Client.ExecuteTransactionAsync(_ => completion.Task, TimeSpan.FromMilliseconds(100));
        await TestAssert.FailureAsync(() => request, CommunicationFailure.Timeout);
        TestAssert.True(!pair.Client.IsConnected);
        completion.SetResult(true);
    }

    internal static async Task TransactionLifetimeAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        ITcpTransaction transaction = await pair.Client.ExecuteTransactionAsync(current => Task.FromResult(current), TcpFixture.Timeout);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => transaction.SendAsync([1]));
    }

    internal static async Task ParallelTransactionMisuseAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            Task<byte[]> first = transaction.ReceiveAsync(OneByte);
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => transaction.ReceiveAsync(OneByte));
            await TcpFixture.WriteAsync(pair.Peer, [1]);
            TestAssert.Bytes([1], await first);
            return true;
        }, TcpFixture.Timeout);
    }

    internal static async Task ConnectionRefusedAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        IPEndPoint address = (IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        using var client = new TcpClient(address, OneByte);
        var error = await TestAssert.FailureAsync(() => client.OpenAsync(TcpFixture.Timeout), CommunicationFailure.SocketError);
        TestAssert.Equal(CommunicationStage.Connecting, error.Stage);
        TestAssert.True(error.SocketErrorCode.HasValue);
        TestAssert.True(!client.IsConnected);
    }

    internal static async Task PreCancelledConnectAsync()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), OneByte);
        await TestAssert.FailureAsync(() => client.OpenAsync(TcpFixture.Timeout, cancellation.Token), CommunicationFailure.Cancelled);
    }

    internal static async Task Ipv6Async()
    {
        using var pair = await TcpFixture.CreateAsync(address: IPAddress.IPv6Loopback);
        Task<byte[]> response = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [2]);
        TestAssert.Bytes([2], await response);
    }

    internal static async Task CaughtFrameFailureStillInvalidatesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [4, 0, 0, 4]);
        await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            try { await transaction.ReceiveAsync(FrameTests.S7); }
            catch (InvalidDataException) { /* 장치 코드가 오류를 잡아도 성공으로 바뀌면 안 된다. */ }
            return true;
        }, TcpFixture.Timeout), CommunicationFailure.InvalidFrame);
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task DisconnectDuringReceiveAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> request = pair.Client.ExchangeAsync([1], OneByte, TcpFixture.Timeout);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        pair.Client.Close();
        CommunicationException error = await TestAssert.ThrowsAsync<CommunicationException>(() => request);
        TestAssert.True(error.Failure == CommunicationFailure.ConnectionClosed || error.Failure == CommunicationFailure.SocketError);
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task CompletedDeadlineDoesNotCloseConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await pair.Client.SendAsync([1], TimeSpan.FromMilliseconds(100));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await Task.Delay(180);
        await pair.Client.SendAsync([2], TcpFixture.Timeout);
        TestAssert.Bytes([2], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
    }

    internal static async Task DuplicateConnectPreservesConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.OpenAsync(TcpFixture.Timeout));
        TestAssert.True(pair.Client.IsConnected);
        await pair.Client.SendAsync([1], TcpFixture.Timeout);
        TestAssert.Bytes([1], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
    }

    internal static async Task UnawaitedTransactionOperationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]>? unfinished = null;
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.ExecuteTransactionAsync(transaction =>
        {
            unfinished = transaction.ReceiveAsync(OneByte);
            return Task.FromResult(true);
        }, TcpFixture.Timeout));
        TestAssert.True(!pair.Client.IsConnected);
        await TestAssert.ThrowsAsync<Exception>(() => unfinished!);
    }

    internal static async Task ContinuousSendDeadlineAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        pair.Peer.ReceiveBufferSize = 1024;
        byte[] payload = new byte[8192];
        // OS마다 루프백 버퍼링이 다르므로 단일 Send가 반드시 막힌다고 가정하지 않는다.
        // 연속 송신 작업도 최초 마감 시간에 종료되는지 실제 소켓으로 확인한다.
        var error = await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync<bool>(async transaction =>
        {
            while (true) await transaction.SendAsync(payload);
        }, TimeSpan.FromMilliseconds(120)), CommunicationFailure.Timeout);
        TestAssert.Equal(CommunicationStage.Sending, error.Stage);
        TestAssert.True(!pair.Client.IsConnected);
    }
}
