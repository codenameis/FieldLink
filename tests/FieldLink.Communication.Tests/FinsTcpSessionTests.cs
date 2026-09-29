using System.Net.Sockets;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Omron.Clients;

namespace FieldLink.Communication.Tests;

internal static class FinsTcpSessionTests
{
    // W421 §7-4-2 외피와 D100 두 워드 읽기. 제품 빌더를 기대값으로 사용하지 않는다.
    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
    private static byte[] ReadCommand => H("0101820064000002");
    private static byte[] NodeRequest => H("46494E530000000C000000000000000000000000");
    private static byte[] NodeReply => H("46494E530000001000000001000000000000000D00000001");
    private static byte[] ReadRequest => H("46494E530000001A0000000200000000800002000100000D00000101820064000002");
    private static byte[] ReadReply => H("46494E530000001A0000000200000000C00002000D0000010000010100001234ABCD");

    private static async Task NegotiateAsync(Socket peer, byte[]? reply = null)
    {
        TestAssert.Bytes(NodeRequest, await TcpFixture.ReadExactlyAsync(peer, 20));
        await TcpFixture.WriteAsync(peer, reply ?? NodeReply);
    }

    private static async Task CompleteReadAsync(Socket peer, Task<OperationResult<byte[]>> pending,
        byte sid = 0, byte clientNode = 13, byte serverNode = 1)
    {
        byte[] request = ReadRequest;
        request[20] = serverNode;
        request[23] = clientNode;
        request[25] = sid;
        TestAssert.Bytes(request, await TcpFixture.ReadExactlyAsync(peer, request.Length));
        byte[] response = ReadReply;
        response[20] = clientNode;
        response[23] = serverNode;
        response[25] = sid;
        await TcpFixture.WriteAsync(peer, response);
        var result = await pending;
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Bytes(H("1234ABCD"), result.Content);
    }

    internal static async Task ReconnectReplacesNodeAddressesAndResetsSidAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var first = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(pair.Peer);
        await CompleteReadAsync(pair.Peer, first);
        await CompleteReadAsync(pair.Peer, session.ExchangeAsync(ReadCommand), sid: 1);
        pair.Client.Close();
        TestAssert.True(!session.IsInitialized);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        var second = session.ExchangeAsync(ReadCommand);
        var response = NodeReply;
        response[19] = 14;
        response[23] = 2;
        await NegotiateAsync(peer, response);
        await CompleteReadAsync(peer, second, clientNode: 14, serverNode: 2);
        TestAssert.True(session.IsInitialized);
    }

    internal static async Task ConcurrentRequestsNegotiateOnlyOnceAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var first = session.ExchangeAsync(ReadCommand);
        var second = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(pair.Peer);
        await CompleteReadAsync(pair.Peer, first);
        await CompleteReadAsync(pair.Peer, second, sid: 1);
    }

    internal static async Task NegotiationErrorClosesConnectionAndPreservesCodeAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var first = session.ExchangeAsync(ReadCommand);
        byte[] rejected = NodeReply;
        rejected[15] = 0x21;
        await NegotiateAsync(pair.Peer, rejected);
        var result = await first;
        TestAssert.True(!result.IsSuccess);
        TestAssert.Equal(0x21, result.ErrorCode);
        TestAssert.True(!session.IsInitialized);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        TestAssert.Equal(0, await pair.Peer.ReceiveAsync(new byte[1], SocketFlags.None));
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        var next = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(peer);
        await CompleteReadAsync(peer, next);
    }

    internal static async Task MalformedNegotiationCannotCreateSessionAsync()
    {
        var cases = new List<byte[]>();
        byte[] shortReply = TestBytes.Slice(NodeReply, 0, 20); shortReply[7] = 12; cases.Add(shortReply);
        byte[] wrongCommand = NodeReply; wrongCommand[11] = 2; cases.Add(wrongCommand);
        byte[] zeroNode = NodeReply; zeroNode[19] = 0; cases.Add(zeroNode);
        byte[] highNode = NodeReply; highNode[18] = 1; cases.Add(highNode);
        byte[] broadcastNode = NodeReply; broadcastNode[23] = 255; cases.Add(broadcastNode);
        foreach (var response in cases)
        {
            using var pair = await TcpFixture.CreateAsync();
            var session = new FinsTcpSession(pair.Client);
            var pending = session.ExchangeAsync(ReadCommand);
            await NegotiateAsync(pair.Peer, response);
            await TestAssert.FailureAsync(() => pending, CommunicationFailure.InvalidFrame);
            TestAssert.True(!session.IsInitialized);
        }
    }

    internal static async Task ExplicitInitializationAndFirstCommandShareSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        Task<OperationResult> initialized = session.InitializeAsync();
        await NegotiateAsync(pair.Peer);
        TestAssert.True((await initialized).IsSuccess && session.IsInitialized);
        TestAssert.True((await session.InitializeAsync()).IsSuccess);
        await CompleteReadAsync(pair.Peer, session.ExchangeAsync(ReadCommand));
    }

    internal static async Task CommandRejectionKeepsSessionAndAdvancesSidAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var pending = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(pair.Peer);
        TestAssert.Bytes(ReadRequest, await TcpFixture.ReadExactlyAsync(pair.Peer, ReadRequest.Length));
        await TcpFixture.WriteAsync(pair.Peer, H("46494E53000000160000000200000000C00002000D000001000001012101"));
        var rejected = await pending;
        TestAssert.True(!rejected.IsSuccess);
        TestAssert.Equal(0x2101, rejected.ErrorCode);
        TestAssert.True(session.IsInitialized);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        await CompleteReadAsync(pair.Peer, session.ExchangeAsync(ReadCommand), sid: 1);
    }

    internal static async Task TcpErrorNotificationInvalidatesSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var pending = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(pair.Peer);
        await TcpFixture.ReadExactlyAsync(pair.Peer, ReadRequest.Length);
        await TcpFixture.WriteAsync(pair.Peer, H("46494E53000000080000000300000003"));
        var rejected = await pending;
        TestAssert.True(!rejected.IsSuccess && !session.IsInitialized);
        TestAssert.Equal(3, rejected.ErrorCode);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }

    internal static async Task StaleSidAndConnectionConfirmationDoNotCompleteRequestAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var pending = session.ExchangeAsync(ReadCommand);
        TestAssert.Bytes(NodeRequest, await TcpFixture.ReadExactlyAsync(pair.Peer, 20));
        // TCP 헤더가 나뉘어 수신되어도 협상 후 명령을 전송한다.
        await TcpFixture.WriteAsync(pair.Peer, TestBytes.Slice(NodeReply, 0, 7));
        await TcpFixture.WriteAsync(pair.Peer, TestBytes.Slice(NodeReply, 7));
        await TcpFixture.ReadExactlyAsync(pair.Peer, ReadRequest.Length);
        byte[] stale = ReadReply; stale[25] = 255; stale[30] = 0xFF;
        byte[] notification = H("46494E53000000080000000600000000");
        await TcpFixture.WriteAsync(pair.Peer, [.. notification, .. stale, .. ReadReply]);
        var result = await pending;
        TestAssert.True(result.IsSuccess);
        TestAssert.Bytes(H("1234ABCD"), result.Content);
        TestAssert.True(session.IsInitialized);
    }

    internal static async Task ActiveCancellationRequiresFreshNegotiationAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        using var cancellation = new CancellationTokenSource();
        var pending = session.ExchangeAsync(ReadCommand, cancellation.Token);
        TestAssert.Bytes(NodeRequest, await TcpFixture.ReadExactlyAsync(pair.Peer, 20));
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => pending, CommunicationFailure.Cancelled);
        TestAssert.True(!session.IsInitialized);
        await pair.Client.OpenAsync();
        using var peer = await pair.Listener.AcceptSocketAsync();
        var next = session.ExchangeAsync(ReadCommand);
        await NegotiateAsync(peer);
        await CompleteReadAsync(peer, next);
    }

    internal static async Task InitializationTimeoutDoesNotLeaveReadySessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client, TimeSpan.FromMilliseconds(150));
        var pending = session.InitializeAsync();
        TestAssert.Bytes(NodeRequest, await TcpFixture.ReadExactlyAsync(pair.Peer, 20));
        await TestAssert.FailureAsync(() => pending, CommunicationFailure.Timeout);
        TestAssert.True(!session.IsInitialized);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => session.ExchangeAsync(ReadCommand));
        TestAssert.True(!pair.Listener.Pending());
    }

    internal static async Task WrongRouteOrCommandInvalidatesSessionAsync()
    {
        foreach (int field in new[] { 19, 20, 21, 22, 23, 24, 26, 27 })
        {
            using var pair = await TcpFixture.CreateAsync();
            var session = new FinsTcpSession(pair.Client);
            var pending = session.ExchangeAsync(ReadCommand);
            await NegotiateAsync(pair.Peer);
            await TcpFixture.ReadExactlyAsync(pair.Peer, ReadRequest.Length);
            byte[] response = ReadReply; response[field]++;
            await TcpFixture.WriteAsync(pair.Peer, response);
            // 외피 파싱 실패와 구분한다. 응답 판정기의 Reject는 기존 전송 계약상 ResponseRejected다.
            await TestAssert.FailureAsync(() => pending, CommunicationFailure.ResponseRejected);
            TestAssert.True(!session.IsInitialized);
        }
    }

    internal static async Task SidWrapsWithinTheSameSessionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        var initial = session.InitializeAsync();
        await NegotiateAsync(pair.Peer);
        await initial;
        for (int sid = 0; sid <= 256; sid++)
            await CompleteReadAsync(pair.Peer, session.ExchangeAsync(ReadCommand), unchecked((byte)sid));
    }

    internal static async Task InvalidInputsAndDisposedTransportDoNotNegotiateAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var session = new FinsTcpSession(pair.Client);
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => session.ExchangeAsync(null!));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ExchangeAsync(new byte[1]));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ExchangeAsync(new byte[2003]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => session.InitializeAsync(cancellation.Token), CommunicationFailure.Cancelled);
        TestAssert.Equal(0, pair.Peer.Available);
        pair.Client.Dispose();
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => session.InitializeAsync());
        TestAssert.True(!session.IsInitialized);
    }
}
