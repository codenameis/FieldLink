using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.PlcDrivers.Melsec;

namespace FieldLink.Communication.Tests;

internal static class MelsecMc3EBinaryClientTests
{
    internal static async Task ReadsD100ThroughD149WithFixedRequestAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        ushort[] memory = Enumerable.Range(0, 200).Select(value => (ushort)value).ToArray();
        Task<MelsecQnA3EPlc.Exchange> server = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        var result = await plc.ReadWordsAsync("D100", 50, deadline.Token);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000640000A83200"), (await server).Request);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal(50, result.Content.Length);
        for (int i = 0; i < 50; i++) TestAssert.Equal((ushort)(100 + i), result.Content[i]);
    }

    internal static async Task DeviceErrorPreservesCodeAndConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        ushort[] memory = new ushort[200];
        Task<MelsecQnA3EPlc.Exchange> rejected = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        var failure = await plc.ReadWordsAsync("D190", 50, deadline.Token);
        await rejected;
        TestAssert.True(!failure.IsSuccess);
        TestAssert.Equal(0xC056, failure.ErrorCode);
        TestAssert.True(pair.Client.IsConnected);
        memory[100] = 0xABCD;
        Task<MelsecQnA3EPlc.Exchange> accepted = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        var success = await plc.ReadWordsAsync("D100", 1, deadline.Token);
        await accepted;
        TestAssert.True(success.IsSuccess, success.Message);
        TestAssert.Equal((ushort)0xABCD, success.Content[0]);
        TestAssert.Equal(1L, pair.Client.ConnectionGeneration);
    }

    internal static async Task MissingWordDataRejectsResponseAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        var pending = plc.ReadWordsAsync("D100", 2);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        // 실제 길이에 맞는 외피를 사용한다. 성공 데이터만 한 워드 부족하다.
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000003412"));
        await TestAssert.FailureAsync(() => pending, CommunicationFailure.ResponseRejected);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }

    internal static async Task WrongSubheaderRejectsFrameAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        var pending = plc.ReadWordsAsync("D100", 1);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D10000FFFF0300040000003412"));
        await TestAssert.FailureAsync(() => pending, CommunicationFailure.InvalidFrame);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }

    internal static async Task RouteSnapshotSurvivesCallerChangesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var route = new McFrameOptions { NetworkNumber = 1, PLCNumber = 2, TargetIOStation = 0x1234, NetworkStationNumber = 5 };
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client, route);
        route.NetworkNumber = 9; route.PLCNumber = 9; route.TargetIOStation = 9; route.NetworkStationNumber = 9;
        var pending = plc.ReadWordsAsync("W10", 1);
        TestAssert.Bytes(H("500001023412050C000A0001040000100000B40100"), await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D000010234120504000000FFFF"));
        var result = await pending;
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal(ushort.MaxValue, result.Content[0]);
    }

    internal static async Task InvalidResponseLengthsAndRoutesAreRejectedAsync()
    {
        foreach (string reply in new[]
        {
            "D00001FFFF0300040000003412", // 다른 네트워크
            "D00000FFFF030002000000",     // 정상 종료이지만 데이터 없음
            "D00000FFFF03000600000034125678", // 요청보다 많은 데이터
            "D00000FFFF0300050000003412FF"    // 홀수 데이터 바이트
        })
        {
            using var pair = await TcpFixture.CreateAsync();
            var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
            var pending = plc.ReadWordsAsync("D100", 1);
            await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
            await TcpFixture.WriteAsync(pair.Peer, H(reply));
            await TestAssert.FailureAsync(() => pending, CommunicationFailure.ResponseRejected);
            TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        }
    }

    internal static async Task ErrorDetailsDoNotBecomeWordPayloadAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        var pending = plc.ReadWordsAsync("D100", 50);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        // 부가 오류 정보의 발생지 경로는 요청 경로와 달라도 된다(SH-080008 §5.3).
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000B0056C0010234120501040000"));
        var result = await pending;
        TestAssert.True(!result.IsSuccess);
        TestAssert.Equal(0xC056, result.ErrorCode);
        TestAssert.True(pair.Client.IsConnected);
    }

    internal static async Task InvalidInputsDoNotSendRequestsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => plc.ReadWordsAsync(null!, 1));
        await TestAssert.ThrowsAsync<ArgumentException>(() => plc.ReadWordsAsync(" ", 1));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadWordsAsync("D100", 0));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadWordsAsync("D100", 951));
        foreach (string address in new[] { "D", "D-1", "D16777216", "D16777215", "M16777201", "ext=1;W0" })
            TestAssert.True(!(await plc.ReadWordsAsync(address, 2)).IsSuccess, address);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => plc.ReadWordsAsync("D100", 1, cancelled.Token));
        TestAssert.Equal(0, pair.Peer.Available);
        TestAssert.True(pair.Client.IsConnected);
    }

    internal static async Task ReadUpperBoundAndBitDeviceWordUnitAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        ushort[] memory = Enumerable.Range(0, 950).Select(value => (ushort)value).ToArray();
        Task<MelsecQnA3EPlc.Exchange> server = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        var maximum = await plc.ReadWordsAsync("D0", 950, deadline.Token);
        await server;
        TestAssert.True(maximum.IsSuccess, maximum.Message);
        TestAssert.Equal(950, maximum.Content.Length);
        TestAssert.Equal((ushort)949, maximum.Content[949]);
        var bitWords = plc.ReadWordsAsync("M100", 2, deadline.Token);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000640000900200"), await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000600000000800100"));
        var bitResult = await bitWords;
        TestAssert.True(bitResult.IsSuccess, bitResult.Message);
        TestAssert.Equal((ushort)0x8000, bitResult.Content[0]);
        TestAssert.Equal((ushort)1, bitResult.Content[1]);
        var lastAddress = plc.ReadWordsAsync("D16777215", 1, deadline.Token);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000FFFFFFA80100"), await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000000100"));
        TestAssert.True((await lastAddress).IsSuccess);
    }

    internal static async Task ConcurrentReadsKeepResponsesAndArraysSeparateAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        var first = plc.ReadWordsAsync("D100", 1);
        var second = plc.ReadWordsAsync("D101", 1);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000640000A80100"), await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        TestAssert.Equal(0, pair.Peer.Available);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000"));
        await TcpFixture.WriteAsync(pair.Peer, H("FFFF0300040000003412"));
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000650000A80100"), await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030004000000CDAB"));
        var firstResult = await first;
        var secondResult = await second;
        TestAssert.True(firstResult.IsSuccess && secondResult.IsSuccess);
        TestAssert.Equal((ushort)0x1234, firstResult.Content[0]);
        firstResult.Content[0] = 0;
        TestAssert.Equal((ushort)0xABCD, secondResult.Content[0]);
    }

    internal static async Task CloseAndDisposeDoNotTriggerReconnectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        pair.Client.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadWordsAsync("D100", 1));
        TestAssert.True(!pair.Listener.Pending());
        TestAssert.Equal(1L, pair.Client.ConnectionGeneration);
        pair.Client.Dispose();
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => plc.ReadWordsAsync("D100", 1));
    }

    internal static async Task TimeoutIsNotReturnedAsDeviceFailureAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client, timeout: TimeSpan.FromMilliseconds(300));
        var pending = plc.ReadWordsAsync("D100", 1);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        // 선언한 본문이 잘려 도착한 상태로 응답을 중단한다.
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000400000034"));
        var error = await TestAssert.FailureAsync(() => pending, CommunicationFailure.Timeout);
        TestAssert.Equal(21L, error.BytesSent);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadWordsAsync("D100", 1));
        TestAssert.True(!pair.Listener.Pending());
    }

    internal static async Task ActiveCancellationPropagatesCommunicationFailureAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var plc = new MelsecMc3EBinaryTcpClient(pair.Client);
        var pending = plc.ReadWordsAsync("D100", 1, cancellation.Token);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        cancellation.Cancel();
        await TestAssert.FailureAsync(() => pending, CommunicationFailure.Cancelled);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }

    internal static async Task FrameBoundaryChecksMinimumLengthAndSegmentOffsetAsync()
    {
        var frame = new MelsecMc3EBinaryFrame();
        byte[] response = H("D00000FFFF030066000000");
        TestAssert.True(!frame.GetFrameLength(new ArraySegment<byte>(response, 0, 8)).HasValue);
        byte[] buffered = [0xAA, 0xBB, ..response];
        TestAssert.Equal<int?>(111, frame.GetFrameLength(new ArraySegment<byte>(buffered, 2, response.Length)));
        foreach (string invalid in new[] { "D00000FFFF03000000", "D00000FFFF03000100" })
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(frame.GetFrameLength(new ArraySegment<byte>(H(invalid)))));
    }

    internal static async Task SampleReadsD100AndClosesItsTransportAsync()
    {
        using var listener = new TestTcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var pending = FieldLink.Samples.MelsecReadWordsExample.ReadD100Async((System.Net.IPEndPoint)listener.LocalEndpoint, deadline.Token);
        using var peer = await listener.AcceptSocketAsync(deadline.Token);
        ushort[] memory = Enumerable.Range(0, 200).Select(value => (ushort)value).ToArray();
        await MelsecQnA3EPlc.ServeOnceAsync(peer, memory, deadline.Token);
        var result = await pending;
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal((ushort)100, result.Content[0]);
        TestAssert.Equal((ushort)149, result.Content[49]);
        TestAssert.Equal(0, await peer.ReceiveAsync(new byte[1], System.Net.Sockets.SocketFlags.None, deadline.Token));
    }

    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
}
