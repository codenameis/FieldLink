using System.Net;
using System.Text;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.Communication.Diagnostics;

namespace FieldLink.Communication.Tests;

internal static partial class McClientTests
{
    internal static async Task AllNumericScalarAndArrayOverloadsUseNativeBytesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        await CheckNumericAsync(pair, (short)-2, "FEFF",
            () => plc.ReadInt16Async("D0"), () => plc.ReadInt16Async("D0", 2),
            () => plc.WriteAsync("D0", (short)-2), () => plc.WriteAsync("D0", new short[] { -2, -2 }));
        await CheckNumericAsync(pair, (ushort)65000, "E8FD",
            () => plc.ReadUInt16Async("D0"), () => plc.ReadUInt16Async("D0", 2),
            () => plc.WriteAsync("D0", (ushort)65000), () => plc.WriteAsync("D0", new ushort[] { 65000, 65000 }));
        await CheckNumericAsync(pair, -2, "FEFFFFFF",
            () => plc.ReadInt32Async("D0"), () => plc.ReadInt32Async("D0", 2),
            () => plc.WriteAsync("D0", -2), () => plc.WriteAsync("D0", new[] { -2, -2 }));
        await CheckNumericAsync(pair, 0x12345678u, "78563412",
            () => plc.ReadUInt32Async("D0"), () => plc.ReadUInt32Async("D0", 2),
            () => plc.WriteAsync("D0", 0x12345678u), () => plc.WriteAsync("D0", new[] { 0x12345678u, 0x12345678u }));
        await CheckNumericAsync(pair, -2L, "FEFFFFFFFFFFFFFF",
            () => plc.ReadInt64Async("D0"), () => plc.ReadInt64Async("D0", 2),
            () => plc.WriteAsync("D0", -2L), () => plc.WriteAsync("D0", new[] { -2L, -2L }));
        await CheckNumericAsync(pair, 0xFEDCBA9876543210UL, "1032547698BADCFE",
            () => plc.ReadUInt64Async("D0"), () => plc.ReadUInt64Async("D0", 2),
            () => plc.WriteAsync("D0", 0xFEDCBA9876543210UL),
            () => plc.WriteAsync("D0", new[] { 0xFEDCBA9876543210UL, 0xFEDCBA9876543210UL }));
        await CheckNumericAsync(pair, 1.5f, "0000C03F",
            () => plc.ReadFloatAsync("D0"), () => plc.ReadFloatAsync("D0", 2),
            () => plc.WriteAsync("D0", 1.5f), () => plc.WriteAsync("D0", new[] { 1.5f, 1.5f }));
        await CheckNumericAsync(pair, -2.25, "00000000000002C0",
            () => plc.ReadDoubleAsync("D0"), () => plc.ReadDoubleAsync("D0", 2),
            () => plc.WriteAsync("D0", -2.25), () => plc.WriteAsync("D0", new[] { -2.25, -2.25 }));
    }

    private static async Task CheckNumericAsync<T>(TcpFixture pair, T expected, string payload,
        Func<Task<OperationResult<T>>> readOne, Func<Task<OperationResult<T[]>>> readMany,
        Func<Task<OperationResult>> writeOne, Func<Task<OperationResult>> writeMany)
    {
        for (int count = 1; count <= 2; count++)
        {
            string data = count == 1 ? payload : payload + payload;
            int bytes = data.Length / 2;
            int words = bytes / 2;
            Task<OperationResult<T>>? scalar = count == 1 ? readOne() : null;
            Task<OperationResult<T[]>>? array = count == 2 ? readMany() : null;
            TestAssert.Bytes(H("500000FFFF03000C000A0001040000000000A8" + words.ToString("X2") + "00"),
                await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
            await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300" + (bytes + 2).ToString("X2") + "000000" + data));
            if (scalar != null)
            {
                var result = await scalar;
                TestAssert.True(result.IsSuccess, result.Message);
                TestAssert.Equal(expected, result.Content);
            }
            else
            {
                var result = await array!;
                TestAssert.True(result.IsSuccess, result.Message);
                TestAssert.True(result.Content.SequenceEqual(new[] { expected, expected }));
            }
            var write = count == 1 ? writeOne() : writeMany();
            TestAssert.Bytes(H("500000FFFF0300" + (12 + bytes).ToString("X2") + "000A0001140000000000A8" +
                words.ToString("X2") + "00" + data), await TcpFixture.ReadExactlyAsync(pair.Peer, 21 + bytes));
            await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
            TestAssert.True((await write).IsSuccess);
        }
    }

    internal static async Task SplitReadsAdvanceHexAndBitDeviceWordAddressesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions { MaxWordsPerRequest = 1 });
        foreach (var item in new[] { ("WFF", "FF0000B4", "000100B4"), ("M100", "64000090", "74000090") })
        {
            var read = plc.ReadInt32Async(item.Item1);
            TestAssert.Bytes(H("500000FFFF03000C000A0001040000" + item.Item2 + "0100"),
                await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
            await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000007856"));
            TestAssert.Bytes(H("500000FFFF03000C000A0001040000" + item.Item3 + "0100"),
                await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
            await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000003412"));
            TestAssert.Equal(0x12345678, (await read).Content);
        }
    }

    internal static async Task QueuedWriteSnapshotsInputAndQueuedCancelKeepsConnectionAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var blocker = plc.ReadInt16Async("D0");
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        using var cancellation = new CancellationTokenSource();
        var cancelled = plc.ReadInt16Async("D10", cancellation.Token);
        cancellation.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        int[] values = { 1, 2 };
        var write = plc.WriteAsync("D100", values);
        values[0] = 999;
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000000000"));
        TestAssert.True((await blocker).IsSuccess);
        TestAssert.Bytes(H("500000FFFF030014000A0001140000640000A804000100000002000000"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 29));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await write).IsSuccess);
        TestAssert.Equal(ClientState.Open, plc.State);
    }

    internal static async Task SplitCancellationPreservesConfirmedAndUncertainWritesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions { MaxWordsPerRequest = 2 });
        using var cancellation = new CancellationTokenSource();
        var write = plc.WriteAsync("D100", new[] { 1, 2, 3 }, cancellation.Token);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 25);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 25);
        cancellation.Cancel();
        var error = await TestAssert.ThrowsAsync<OperationCanceledException>(() => write);
        var details = ((PlcOperationCanceledException)error).FailureDetails;
        TestAssert.Equal(2, details.ConfirmedCount);
        TestAssert.Equal(2, details.UncertainCount);
        TestAssert.Equal(CommunicationFailure.Cancelled, ((CommunicationException)details.Cause).Failure);
        TestAssert.Equal(ClientState.Faulted, plc.State);
    }

    internal static async Task SplitOperationUsesOneDeadlineAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions
        {
            MaxWordsPerRequest = 1, Timeout = TimeSpan.FromMilliseconds(800)
        });
        var read = plc.ReadInt32Async("D0");
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await Task.Delay(500);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000000100"));
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        TestAssert.True(await Task.WhenAny(read, Task.Delay(600)) == read, "Second block must not restart the timeout.");
        var failed = await read;
        TestAssert.True(!failed.IsSuccess);
        TestAssert.Equal(1, failed.FailureDetails.ConfirmedCount);
        TestAssert.Equal(0, failed.FailureDetails.UncertainCount);
        TestAssert.Equal(CommunicationFailure.Timeout, ((CommunicationException)failed.FailureDetails.Cause).Failure);
    }

    internal static async Task OptionsAreCopiedAndTextUsesExplicitEncodingAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var options = new McClientOptions { ByteOrder = ByteOrder.BigEndian, SwapStringBytes = true };
        using var plc = new MelsecMcClient(pair.Client, options);
        options.ByteOrder = ByteOrder.LittleEndian;
        options.Route.NetworkNumber = 7;
        options.SwapStringBytes = false;
        var number = plc.WriteAsync("D0", 0x12345678);
        TestAssert.Bytes(H("500000FFFF030010000A0001140000000000A8020012345678"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 25));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await number).IsSuccess);
        var text = plc.WriteStringAsync("D0", "가", 4, Encoding.UTF8);
        TestAssert.Bytes(H("500000FFFF030010000A0001140000000000A80200B0EA0080"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 25));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await text).IsSuccess);
        var read = plc.ReadStringAsync("D0", 4, Encoding.UTF8);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030006000000B0EA0080"));
        TestAssert.Equal("가", (await read).Content);
    }

    internal static async Task FailedOpenReturnsCauseAndCanBeExplicitlyRetriedAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        using var plc = Plc.MelsecMcTcp("127.0.0.1", endpoint.Port, new McClientOptions { Timeout = TimeSpan.FromSeconds(3) });
        var failed = await plc.OpenAsync();
        TestAssert.True(!failed.IsSuccess);
        TestAssert.True(failed.FailureDetails.Cause is CommunicationException);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadInt16Async("D0"));
        var retryListener = new TestTcpListener(endpoint);
        retryListener.Start();
        try
        {
            var accepted = retryListener.AcceptSocketAsync();
            TestAssert.True((await plc.OpenAsync()).IsSuccess);
            using var peer = await accepted;
            plc.Close();
            TestAssert.Equal(ClientState.Closed, plc.State);
        }
        finally { retryListener.Stop(); }
    }

    internal static async Task FailureConversionsRetainDetailsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client);
        var reading = plc.ReadInt32Async("D0");
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300020056C0"));
        var failed = await reading;
        TestAssert.True(ReferenceEquals(failed.FailureDetails, failed.ConvertFailed<int>().FailureDetails));
        TestAssert.True(ReferenceEquals(failed.FailureDetails, failed.ConvertFailed<int, int>().FailureDetails));
        TestAssert.True(ReferenceEquals(failed.FailureDetails, failed.ConvertFailed<int, int, int>().FailureDetails));
        var copy = new OperationResult();
        copy.CopyErrorFromOther(failed);
        TestAssert.True(ReferenceEquals(failed.FailureDetails, copy.FailureDetails));
    }

    internal static async Task CompiledSampleWritesReadsAndClosesOwnedConnectionAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var cancellation = new CancellationTokenSource(TcpFixture.Timeout);
            var run = FieldLink.Samples.MelsecTypedExample.WriteAndReadAsync("127.0.0.1",
                ((IPEndPoint)listener.LocalEndpoint).Port, cancellation.Token);
            using var peer = await listener.AcceptSocketAsync(cancellation.Token);
            TestAssert.Bytes(H("500000FFFF030018000A0001140000640000A806000A000000140000001E000000"),
                await TcpFixture.ReadExactlyAsync(peer, 33));
            await TcpFixture.WriteAsync(peer, H("D00000FFFF030002000000"));
            TestAssert.Bytes(H("500000FFFF03000C000A0001040000640000A80600"),
                await TcpFixture.ReadExactlyAsync(peer, 21));
            await TcpFixture.WriteAsync(peer, H("D00000FFFF03000E0000000A000000140000001E000000"));
            var result = await run;
            TestAssert.True(result.IsSuccess, result.Message);
            TestAssert.True(result.Content.SequenceEqual(new[] { 10, 20, 30 }));
            TestAssert.Equal(0, await peer.ReceiveAsync(new byte[1], System.Net.Sockets.SocketFlags.None, cancellation.Token));
        }
        finally { listener.Stop(); }
    }

    internal static async Task OddBitBlocksKeepPaddingSeparateAndExcludeOtherRequestsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var plc = new MelsecMcClient(pair.Client, new McClientOptions { MaxBitsPerRequest = 3 });
        var write = plc.WriteAsync("M100", new[] { true, false, true, false, true });
        TestAssert.Bytes(H("500000FFFF03000E000A00011401006400009003001010"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 23));
        var other = plc.ReadInt16Async("D0");
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.Bytes(H("500000FFFF03000D000A000114010067000090020001"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 22));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF030002000000"));
        TestAssert.True((await write).IsSuccess);
        TestAssert.Bytes(H("500000FFFF03000C000A0001040000000000A80100"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000000100"));
        TestAssert.Equal((short)1, (await other).Content);

        var read = plc.ReadBoolAsync("M100", 5);
        await TcpFixture.ReadExactlyAsync(pair.Peer, 21);
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF0300040000001010"));
        TestAssert.Bytes(H("500000FFFF03000C000A0001040100670000900200"),
            await TcpFixture.ReadExactlyAsync(pair.Peer, 21));
        await TcpFixture.WriteAsync(pair.Peer, H("D00000FFFF03000300000001"));
        TestAssert.True((await read).Content.SequenceEqual(new[] { true, false, true, false, true }));
    }

    internal static async Task InvalidOptionsAndEndpointsAreRejectedWithoutIoAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        foreach (var options in new[]
        {
            new McClientOptions { MaxWordsPerRequest = 0 },
            new McClientOptions { MaxBitsPerRequest = 951 },
            new McClientOptions { Timeout = TimeSpan.Zero },
            new McClientOptions { ByteOrder = (ByteOrder)99 },
            new McClientOptions { Route = null! },
            new McClientOptions { StringEncoding = null! }
        })
            await TestAssert.ThrowsAsync<ArgumentException>(() =>
            {
                using var invalid = new MelsecMcClient(pair.Client, options);
                return Task.CompletedTask;
            });
        await TestAssert.ThrowsAsync<ArgumentException>(() =>
        {
            using var invalid = Plc.MelsecMcTcp("not-an-ip", 5000);
            return Task.CompletedTask;
        });
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            using var invalid = Plc.MelsecMcTcp("127.0.0.1", 0);
            return Task.CompletedTask;
        });
        TestAssert.Equal(ClientState.Open, pair.Client.State);
        TestAssert.Equal(0, pair.Peer.Available);
    }
}
