using System.Net;
using System.Text;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.Communication.Diagnostics;

namespace FieldLink.Communication.Tests;

internal static partial class McClientTests
{
    internal static async Task Int32ArrayUsesOneReadAndOneWriteWithFixedWireBytesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var reading = plc.ReadInt32Async("D100", 2);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000640000A80400"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000A000000FEFFFFFF78563412"));
        var read = await reading;
        TestAssert.True(read.IsSuccess, read.Message);
        TestAssert.Equal(-2, read.Content[0]);
        TestAssert.Equal(0x12345678, read.Content[1]);

        var writing = plc.WriteAsync("D200", new[] { -2, 0x12345678 });
        TestAssert.Bytes(H("500000FFFF030014000A0001140000C80000A80400FEFFFFFF78563412"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 29));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await writing).IsSuccess);
    }

    internal static async Task FactoryOwnsTransportWhileInjectionBorrowsItAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var borrowed = new MelsecMcClient(pair.Client);
        borrowed.Dispose();
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => borrowed.ReadInt32Async("D0"));

        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var owned = Plc.MelsecMcTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            TestAssert.True((await owned.OpenAsync()).IsSuccess);
            using var peer = await accepted;
            owned.Dispose();
            TestAssert.Equal(ClientState.Disposed, owned.State);
        }
        finally
        {
            listener.Stop();
        }
    }

    internal static async Task DeviceRejectionRetainsCodeAndConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var pending = plc.WriteAsync("D100", (short)42);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 23);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300020056C0"));
        var result = await pending;
        TestAssert.True(!result.IsSuccess);
        TestAssert.Equal(0xC056, result.ErrorCode);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
    }

    internal static async Task StringCapacityPaddingEncodingAndReadLengthAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var writing = plc.WriteStringAsync("D100", "ABC", byteLength: 6, encoding: Encoding.ASCII);
        TestAssert.Bytes(H("500000FFFF030012000A0001140000640000A80300414243000000"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 27));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await writing).IsSuccess);

        var reading = plc.ReadStringAsync("D100", byteLength: 6);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030008000000414243000000"));
        TestAssert.Equal("ABC", (await reading).Content);
        TestAssert.True(!(await plc.WriteStringAsync("D0", "ABCDE", 4)).IsSuccess);
        TestAssert.True(!(await plc.WriteStringAsync("D0", "가", 4, Encoding.ASCII)).IsSuccess);
        TestAssert.True(!(await plc.WriteStringAsync("D0", "가", 2, Encoding.UTF8)).IsSuccess);
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadStringAsync("D0", 3));
        TestAssert.Equal(0, pair.Peer.Available);
    }

    internal static async Task BitBatchPacksOddCountAndRejectsInvalidNibbleAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var writing = plc.WriteAsync("M100", new[] { true, false, true });
        TestAssert.Bytes(H("500000FFFF03000E000A00011401006400009003001010"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 23));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await writing).IsSuccess);
        var reading = plc.ReadBoolAsync("M100", 3);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040100640000900300"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000001010"));
        TestAssert.True((await reading).Content.SequenceEqual(new[] { true, false, true }));
        var malformed = plc.ReadBoolAsync("M100", 1);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000300000020"));
        var failed = await malformed;
        TestAssert.True(!failed.IsSuccess);
        TestAssert.True(failed.FailureDetails.Cause is CommunicationException);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
    }

    internal static async Task SplitWriteStopsOnDeviceErrorAndKeepsConfirmedProgressAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions { MaxWordsPerRequest = 2 });
        var writing = plc.WriteAsync("D100", new[] { 1, 2, 3 });
        TestAssert.Bytes(H("500000FFFF030010000A0001140000640000A8020001000000"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 25));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.Bytes(H("500000FFFF030010000A0001140000660000A8020002000000"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 25));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300020056C0"));
        var failed = await writing;
        TestAssert.Equal(0xC056, failed.ErrorCode);
        TestAssert.Equal(2, failed.FailureDetails.ConfirmedCount);
        TestAssert.Equal(0, failed.FailureDetails.UncertainCount);
        TestAssert.Equal("word", failed.FailureDetails.Unit);
        TestAssert.Equal(0, pair.Peer.Available);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
    }

    internal static async Task SplitWriteTimeoutRetainsCauseAndDoesNotRetryAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions
        {
            MaxWordsPerRequest = 2, Timeout = TimeSpan.FromMilliseconds(180)
        });
        var writing = plc.WriteAsync("D100", new[] { 1, 2, 3 });
        await TcpFixture.ReadExactlyAsync(pair.Peer, 25);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 25);
        var failed = await writing;
        TestAssert.True(!failed.IsSuccess);
        var cause = (CommunicationException)failed.FailureDetails.Cause;
        TestAssert.Equal(CommunicationFailure.Timeout, cause.Failure);
        TestAssert.Equal(2, failed.FailureDetails.ConfirmedCount);
        TestAssert.Equal(2, failed.FailureDetails.UncertainCount);
        TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        TestAssert.Equal(0, pair.Peer.Available);
    }

    internal static async Task InputValidationDoesNotSendOrChangeConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadInt32Async("D0", 0));
        await TestAssert.ThrowsAsync<ArgumentException>(() => plc.ReadInt32Async(""));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => plc.WriteAsync("D0", (int[])null!));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.WriteAsync("D0", Array.Empty<int>()));
        TestAssert.True(!(await plc.ReadInt32Async("D16777215")).IsSuccess);
        TestAssert.True(!(await plc.ReadBoolAsync("D0")).IsSuccess);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => plc.WriteAsync("D0", 1, cancellation.Token));
        TestAssert.Equal(0, pair.Peer.Available);
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        plc.Close();
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadInt32Async("D0"));
    }

    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
}
