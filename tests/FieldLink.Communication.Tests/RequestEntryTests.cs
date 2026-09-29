using System.Net;
using System.Reflection;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using UdpClient = FieldLink.Communication.Udp.UdpClient;

namespace FieldLink.Communication.Tests;

internal static class RequestEntryTests
{
    internal static async Task TcpDuplicateOpenDoesNotWaitForRequestAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        long sequence = pair.Client.LastStateChange.Sequence;
        try
        {
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => pair.Client.OpenAsync(TimeSpan.FromMilliseconds(100)));
            TestAssert.Equal(sequence, pair.Client.LastStateChange.Sequence);
        }
        finally { await TcpFixture.WriteAsync(pair.Peer, [2]); await active; }
        await pair.Client.SendAsync([3]);
        TestAssert.Bytes([3], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
    }

    internal static async Task UdpDuplicateOpenDoesNotWaitForRequestAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        Task<byte[]> active = client.ExchangeAsync([1]);
        var received = await peer.ReceiveAsync();
        long sequence = client.LastStateChange.Sequence;
        try
        {
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync(TimeSpan.FromMilliseconds(100)));
            TestAssert.Equal(sequence, client.LastStateChange.Sequence);
        }
        finally { await peer.SendAsync(new byte[] { 2 }, received.RemoteEndPoint); await active; }
        await client.SendAsync([3]);
        TestAssert.Bytes([3], (await peer.ReceiveAsync()).Buffer);
    }

    internal static async Task TcpRequestsDuringOpenAreRejectedAsync()
    {
        using ITcpClient client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), new FixedLengthFrame(1));
        using IDisposable opening = await BeginOpeningAsync(client);
        long sequence = client.LastStateChange.Sequence;
        TimeSpan timeout = TimeSpan.FromMilliseconds(100);
        bool callbackRan = false;
        try
        {
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync(timeout));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([1], timeout));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeAsync([1], timeout));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExecuteTransactionAsync(_ =>
            {
                callbackRan = true;
                return Task.FromResult(true);
            }, timeout));
            TestAssert.True(!callbackRan);
            TestAssert.Equal(sequence, client.LastStateChange.Sequence);
            TestAssert.Equal(ClientState.Opening, client.State);
            TestAssert.Equal(0L, client.ConnectionGeneration);
        }
        finally { client.Close(); }
    }

    internal static async Task UdpRequestsDuringOpenAreRejectedAsync()
    {
        using IUdpClient client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 1));
        using IDisposable opening = await BeginOpeningAsync(client);
        long sequence = client.LastStateChange.Sequence;
        try
        {
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync(TimeSpan.FromMilliseconds(100)));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([1], TimeSpan.FromMilliseconds(100)));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeAsync([1], TimeSpan.FromMilliseconds(100)));
            TestAssert.Equal(sequence, client.LastStateChange.Sequence);
            TestAssert.Equal(ClientState.Opening, client.State);
        }
        finally { client.Close(); }
    }

    internal static async Task TcpRejectedRequestsDoNotCopyPayloadAsync()
    {
        using ITcpClient client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), new FixedLengthFrame(1));
        byte[] request = new byte[1024 * 1024];
        long before = TestAllocationCounter.GetAllocatedBytesForCurrentThread();
        Task send = client.SendAsync(request);
        long allocated = TestAllocationCounter.GetAllocatedBytesForCurrentThread() - before;
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => send);
        TestAssert.True(allocated < request.Length, $"Rejected send allocated {allocated} bytes for a {request.Length}-byte payload.");
        TestAssert.Equal(ClientState.Closed, client.State);
    }

    internal static async Task UdpRejectedRequestsDoNotCopyPayloadAsync()
    {
        using IUdpClient client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 1));
        byte[] request = new byte[60 * 1024];
        long before = TestAllocationCounter.GetAllocatedBytesForCurrentThread();
        Task exchange = client.ExchangeAsync(request);
        long allocated = TestAllocationCounter.GetAllocatedBytesForCurrentThread() - before;
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => exchange);
        TestAssert.True(allocated < request.Length, $"Rejected exchange allocated {allocated} bytes for a {request.Length}-byte payload.");
        TestAssert.Equal(ClientState.Closed, client.State);
    }

    internal static async Task TcpQueuedRequestsBorrowBuffersAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        byte[] request = Enumerable.Repeat((byte)7, 128 * 1024).ToArray();
        for (int operation = 0; operation < 2; operation++)
        {
            Task<byte[]> active = pair.Client.ExchangeAsync([1]);
            await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
            long before = TestAllocationCounter.GetAllocatedBytesForCurrentThread();
            Task queued = operation == 0 ? pair.Client.SendAsync(request) : pair.Client.ExchangeAsync(request);
            long allocated = TestAllocationCounter.GetAllocatedBytesForCurrentThread() - before;
            TestAssert.True(!queued.IsCompleted);
            await TcpFixture.WriteAsync(pair.Peer, [2]);
            await active;
            TestAssert.Bytes(request, await TcpFixture.ReadExactlyAsync(pair.Peer, request.Length));
            if (operation == 1)
                await TcpFixture.WriteAsync(pair.Peer, [3]);
            await queued;
            TestAssert.True(allocated < request.Length, $"Queued TCP request allocated {allocated} bytes.");
        }
        TestAssert.Equal(ClientState.Open, pair.Client.State);
    }

    internal static async Task UdpQueuedRequestsBorrowBuffersAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        byte[] request = Enumerable.Repeat((byte)7, 60 * 1024).ToArray();
        for (int operation = 0; operation < 2; operation++)
        {
            Task<byte[]> active = client.ExchangeAsync([1]);
            var first = await peer.ReceiveAsync();
            long before = TestAllocationCounter.GetAllocatedBytesForCurrentThread();
            Task queued = operation == 0 ? client.SendAsync(request) : client.ExchangeAsync(request);
            long allocated = TestAllocationCounter.GetAllocatedBytesForCurrentThread() - before;
            TestAssert.True(!queued.IsCompleted);
            await peer.SendAsync(new byte[] { 2 }, first.RemoteEndPoint);
            await active;
            var received = await peer.ReceiveAsync();
            TestAssert.Bytes(request, received.Buffer);
            TestAssert.Equal(first.RemoteEndPoint, received.RemoteEndPoint);
            if (operation == 1)
                await peer.SendAsync(new byte[] { 3 }, received.RemoteEndPoint);
            await queued;
            TestAssert.True(allocated < request.Length, $"Queued UDP request allocated {allocated} bytes.");
        }
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task TcpInvalidRequestsPreserveActiveOperationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        Task<byte[]> active = pair.Client.ExchangeAsync([1]);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        long sequence = pair.Client.LastStateChange.Sequence;
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => pair.Client.SendAsync(null!));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => pair.Client.ExchangeAsync(null!));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => pair.Client.ExchangeAsync([2], null!, TcpFixture.Timeout));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => pair.Client.ExecuteTransactionAsync<bool>(null!));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => pair.Client.SendAsync([2], TimeSpan.Zero));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => pair.Client.ExchangeAsync([2], TimeSpan.MaxValue));
        bool callbackRan = false;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await TestAssert.FailureAsync(() => pair.Client.SendAsync([2], cancelled.Token), CommunicationFailure.Cancelled);
        await TestAssert.FailureAsync(() => pair.Client.ExchangeAsync([2], cancellationToken: cancelled.Token), CommunicationFailure.Cancelled);
        await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(_ =>
        {
            callbackRan = true;
            return Task.FromResult(true);
        }, cancelled.Token), CommunicationFailure.Cancelled);
        TestAssert.True(!callbackRan);
        TestAssert.Equal(sequence, pair.Client.LastStateChange.Sequence);
        await TcpFixture.WriteAsync(pair.Peer, [3]);
        TestAssert.Bytes([3], await active);
        await pair.Client.SendAsync(Array.Empty<byte>());
        await pair.Client.SendAsync([4]);
        TestAssert.Bytes([4], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        TestAssert.Equal(ClientState.Open, pair.Client.State);
    }

    internal static async Task UdpInvalidRequestsPreserveActiveOperationAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!, maximumDatagramLength: 8);
        await client.OpenAsync();
        Task<byte[]> active = client.ExchangeAsync([1]);
        var received = await peer.ReceiveAsync();
        long sequence = client.LastStateChange.Sequence;
        try
        {
            var nullError = await TestAssert.ThrowsAsync<ArgumentNullException>(() => client.ExchangeAsync(null!));
            TestAssert.Equal("request", nullError.ParamName);
            var sizeError = await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExchangeAsync(new byte[9]));
            TestAssert.Equal("request", sizeError.ParamName);
            await TestAssert.ThrowsAsync<ArgumentNullException>(() => client.SendAsync(null!));
            await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendAsync(new byte[9]));
            await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SendAsync([2], TimeSpan.Zero));
            await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExchangeAsync([2], TimeSpan.MaxValue));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await TestAssert.FailureAsync(() => client.SendAsync([2], cancelled.Token), CommunicationFailure.Cancelled);
            await TestAssert.FailureAsync(() => client.ExchangeAsync([2], cancellationToken: cancelled.Token), CommunicationFailure.Cancelled);
            TestAssert.Equal(sequence, client.LastStateChange.Sequence);
        }
        finally { await peer.SendAsync(new byte[] { 3 }, received.RemoteEndPoint); await active; }
        await client.SendAsync(new byte[8]);
        TestAssert.Bytes(new byte[8], (await peer.ReceiveAsync()).Buffer);
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task ClosedAndDisposedRequestsAreRejectedAsync()
    {
        ICommunicationClient[] clients =
        [
            new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), new FixedLengthFrame(1)),
            new UdpClient(new IPEndPoint(IPAddress.Loopback, 1))
        ];
        foreach (ICommunicationClient client in clients)
        {
            using (client)
            {
                bool callbackRan = false;
                long sequence = client.LastStateChange.Sequence;
                await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([1]));
                await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeAsync([1]));
                if (client is ITcpClient tcp)
                    await TestAssert.ThrowsAsync<InvalidOperationException>(() => tcp.ExecuteTransactionAsync(_ =>
                    {
                        callbackRan = true;
                        return Task.FromResult(true);
                    }));
                TestAssert.True(!callbackRan);
                TestAssert.Equal(sequence, client.LastStateChange.Sequence);
                client.Close();
                await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([1]));
                client.Dispose();
                sequence = client.LastStateChange.Sequence;
                await TestAssert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
                await TestAssert.ThrowsAsync<ObjectDisposedException>(() => client.SendAsync([1]));
                await TestAssert.ThrowsAsync<ObjectDisposedException>(() => client.ExchangeAsync([1]));
                if (client is ITcpClient disposedTcp)
                    await TestAssert.ThrowsAsync<ObjectDisposedException>(() => disposedTcp.ExecuteTransactionAsync(_ => Task.FromResult(true)));
                client.Close();
                client.Dispose();
                TestAssert.Equal(ClientState.Disposed, client.State);
                TestAssert.Equal(sequence, client.LastStateChange.Sequence);
            }
        }
    }

    internal static async Task EndedTransactionRejectsWithoutPayloadCopyAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var ended = await pair.Client.ExecuteTransactionAsync(transaction => Task.FromResult(transaction));
        byte[] request = new byte[1024 * 1024];
        long sequence = pair.Client.LastStateChange.Sequence;
        for (int operation = 0; operation < 2; operation++)
        {
            long before = TestAllocationCounter.GetAllocatedBytesForCurrentThread();
            Task rejected = operation == 0 ? ended.SendAsync(request) : ended.ExchangeAsync(request);
            long allocated = TestAllocationCounter.GetAllocatedBytesForCurrentThread() - before;
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => rejected);
            TestAssert.True(allocated < request.Length, $"Ended transaction allocated {allocated} bytes.");
        }
        TestAssert.Equal(sequence, pair.Client.LastStateChange.Sequence);
        await pair.Client.SendAsync([4]);
        TestAssert.Bytes([4], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
    }

    // 실제 내부 Open 절차를 완료 직전에서 멈춘다. OS 접속 속도나 외부 주소에 의존하지 않는다.
    // 상태 필드를 조작하지 않으며 검증 대상 요청은 공개 인터페이스로 호출한다.
    private static async Task<IDisposable> BeginOpeningAsync(ICommunicationClient client)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object connection = client.GetType().GetField("connection", flags)!.GetValue(client)!;
        var task = (Task)connection.GetType().GetMethod("BeginOpenAsync", flags)!
            .Invoke(connection, [TcpFixture.Timeout, CancellationToken.None])!;
        await task;
        return (IDisposable)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
}
