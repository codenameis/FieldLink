using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.Communication.Tests;

internal static class TcpProtocolSessionTests
{
    private sealed class SessionValue(byte value)
    {
        internal byte Value { get; } = value;
    }

    private static TcpProtocolSession<SessionValue> CreateSession(ITcpClient transport) => new(transport,
        async transaction => new SessionValue((await transaction.ExchangeAsync([0x10], new FixedLengthFrame(1)))[0]));

    private static Task<byte[]> ReadAsync(TcpProtocolSession<SessionValue> session,
        CancellationToken token = default) => session.ExecuteAsync(
        (transaction, value) => transaction.ExchangeAsync([0x20, value.Value], new FixedLengthFrame(1)),
        TcpFixture.Timeout, token);

    internal static async Task ReconnectNegotiatesNewSessionBeforeSendingAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = CreateSession(pair.Client);
        Task<byte[]> first = ReadAsync(session);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [42]);
        TestAssert.Bytes([42], await first);
        TestAssert.True(session.IsInitialized);

        pair.Client.Close();
        TestAssert.True(!session.IsInitialized);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.True(!session.IsInitialized);
        Task<byte[]> second = ReadAsync(session);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [9]);
        TestAssert.Bytes([0x20, 9], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [43]);
        TestAssert.Bytes([43], await second);
    }

    internal static async Task ConcurrentCallsInitializeOncePerConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = CreateSession(pair.Client);
        Task<byte[]> first = ReadAsync(session);
        Task<byte[]> second = ReadAsync(session);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
        await TcpFixture.WriteAsync(pair.Peer, [7]);
        for (byte result = 1; result <= 2; result++)
        {
            TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
            await TcpFixture.WriteAsync(pair.Peer, [result]);
        }
        TestAssert.Bytes([1], await first);
        TestAssert.Bytes([2], await second);
    }

    internal static async Task InitializationFailureDoesNotRunCommandAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        int initializations = 0;
        bool commandRan = false;
        var session = new TcpProtocolSession<SessionValue>(pair.Client, _ =>
        {
            if (++initializations == 1)
                throw new InvalidDataException("협상 응답 오류");
            return Task.FromResult(new SessionValue(9));
        });
        await TestAssert.FailureAsync(() => session.ExecuteAsync((_, _) =>
        {
            commandRan = true;
            return Task.FromResult(1);
        }, TcpFixture.Timeout), CommunicationFailure.InvalidFrame);
        TestAssert.True(!commandRan && !session.IsInitialized);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal((byte)9, await session.ExecuteAsync((_, value) => Task.FromResult(value.Value), TcpFixture.Timeout));
        TestAssert.Equal(2, initializations);
    }

    internal static async Task LateInitializationCannotOverwriteNewConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var oldStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldResult = new TaskCompletionSource<SessionValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        int initializations = 0;
        var session = new TcpProtocolSession<SessionValue>(pair.Client, _ =>
        {
            if (++initializations == 1) { oldStarted.SetResult(true); return oldResult.Task; }
            return Task.FromResult(new SessionValue(9));
        });
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> old = ReadAsync(session, cancellation.Token);
        await oldStarted.Task;
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => old, CommunicationFailure.Cancelled);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal((byte)9, await session.ExecuteAsync((_, value) => Task.FromResult(value.Value), TcpFixture.Timeout));
        oldResult.SetResult(new SessionValue(7));
        // 이전 초기화가 늦게 완료되어도 새 연결은 9를 유지하고 재협상하지 않아야 한다.
        Task<byte[]> read = ReadAsync(session);
        TestAssert.Bytes([0x20, 9], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [42]);
        TestAssert.Bytes([42], await read);
        TestAssert.Equal(2, initializations);
        TestAssert.True(session.IsInitialized);
    }

    internal static async Task CloseDuringInitializationRejectsQueuedOldRequestsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = CreateSession(pair.Client);
        Task<byte[]> active = ReadAsync(session);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        Task<byte[]> queued = ReadAsync(session);
        pair.Client.Close();
        Task reopen = pair.Client.OpenAsync();
        await TestAssert.FailureAsync(() => active, CommunicationFailure.ConnectionClosed);
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.ConnectionClosed);
        await reopen;
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.True(!session.IsInitialized);
        Task<byte[]> next = ReadAsync(session);
        TestAssert.Bytes([0x10], await TcpFixture.ReadExactlyAsync(peer, 1));
        await TcpFixture.WriteAsync(peer, [9]);
        TestAssert.Bytes([0x20, 9], await TcpFixture.ReadExactlyAsync(peer, 2));
        await TcpFixture.WriteAsync(peer, [42]);
        TestAssert.Bytes([42], await next);
    }

    internal static async Task QueuedCancellationDoesNotInvalidateActiveSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = CreateSession(pair.Client);
        Task<byte[]> active = ReadAsync(session);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [7]);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> queued = ReadAsync(session, cancellation.Token);
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => queued, CommunicationFailure.Cancelled);
        TestAssert.True(session.IsInitialized);
        await TcpFixture.WriteAsync(pair.Peer, [42]);
        await active;
        Task<byte[]> next = ReadAsync(session);
        TestAssert.Bytes([0x20, 7], await TcpFixture.ReadExactlyAsync(pair.Peer, 2));
        await TcpFixture.WriteAsync(pair.Peer, [43]);
        TestAssert.Bytes([43], await next);
    }

    internal static async Task InvalidCallsDoNotInvokeInitializationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        int initialized = 0;
        var session = new TcpProtocolSession<SessionValue>(pair.Client, _ =>
        {
            initialized++;
            return Task.FromResult(new SessionValue(1));
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => ReadAsync(session, cancellation.Token), CommunicationFailure.Cancelled);
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ExecuteAsync(
            (_, _) => Task.FromResult(1), TimeSpan.Zero));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => session.ExecuteAsync<int>(null!, TcpFixture.Timeout));
        pair.Client.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(session));
        pair.Client.Dispose();
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => ReadAsync(session));
        TestAssert.Equal(0, initialized);
        TestAssert.True(!session.IsInitialized);
    }

    internal static async Task CommandExceptionInvalidatesSessionAndPreservesCauseAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        int initialized = 0;
        var session = new TcpProtocolSession<SessionValue>(pair.Client,
            _ => Task.FromResult(new SessionValue((byte)++initialized)));
        await session.ExecuteAsync((_, value) => Task.FromResult(value.Value), TcpFixture.Timeout);
        var cause = new InvalidOperationException("명령 콜백 오류");
        var error = await TestAssert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync<int>(
            (_, _) => throw cause, TcpFixture.Timeout));
        TestAssert.True(ReferenceEquals(cause, error));
        TestAssert.True(!session.IsInitialized);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        TestAssert.Equal((byte)2, await session.ExecuteAsync((_, value) => Task.FromResult(value.Value), TcpFixture.Timeout));
    }

    internal static async Task NullInitializationResultCannotCreateSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new TcpProtocolSession<SessionValue>(pair.Client, _ => Task.FromResult<SessionValue>(null!));
        bool ran = false;
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync((_, _) =>
        {
            ran = true;
            return Task.FromResult(1);
        }, TcpFixture.Timeout));
        TestAssert.True(!ran && !session.IsInitialized);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }
}
