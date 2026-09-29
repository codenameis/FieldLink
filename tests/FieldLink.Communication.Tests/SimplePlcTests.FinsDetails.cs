using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Omron;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static Task FinsSplitWriteReportsRejectionAndUsesWordOrderAsync() => WithPlcAsync(
        p => Plc.OmronFinsTcp("127.0.0.1", p, new FinsTcpClientOptions { MaxWordsPerRequest = 2 }), async (plc, peer) =>
        {
            var write = plc.WriteAsync("D100", new[] { 0x12345678, 0x23456789, 3 });
            await ExchangeExpectedAsync(peer, "46494E530000001E0000000200000000800002000100000D0000010282006400000256781234",
                "46494E53000000160000000200000000C00002000D000001000001020000");
            await ExchangeExpectedAsync(peer, "46494E530000001E0000000200000000800002000100000D0001010282006600000267892345",
                "46494E53000000160000000200000000C00002000D000001000101022105");
            var result = await write;
            TestAssert.True(!result.IsSuccess);
            TestAssert.Equal(0x2105, result.ErrorCode);
            TestAssert.Equal(2, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(0, result.FailureDetails.UncertainCount);
            TestAssert.Equal(ClientState.Open, plc.State);
        }, InitializeFinsAsync);

    internal static Task FinsBitArrayAndWarningCodeSurviveTypedConversionAsync() => WithPlcAsync(
        p => Plc.OmronFinsTcp("127.0.0.1", p), async (plc, peer) =>
        {
            var bits = plc.ReadBoolAsync("D100.15", 2);
            await ExchangeExpectedAsync(peer, "46494E530000001A0000000200000000800002000100000D000001010200640F0002",
                "46494E53000000180000000200000000C00002000D0000010000010100400100");
            var read = await bits;
            TestAssert.True(read.IsSuccess);
            TestAssert.Equal(0x0040, read.ErrorCode); // W342: CPU 경고는 subcode의 bit 6이다.
            TestAssert.True(read.Content.SequenceEqual(new[] { true, false }));
            var write = plc.WriteAsync("D100.15", new[] { true, false });
            await ExchangeExpectedAsync(peer, "46494E530000001C0000000200000000800002000100000D000101020200640F00020100",
                "46494E53000000160000000200000000C00002000D000001000101020000");
            TestAssert.True((await write).IsSuccess);
            var text = plc.WriteStringAsync("D0", "AB", 4);
            await ExchangeExpectedAsync(peer, "46494E530000001E0000000200000000800002000100000D0002010282000000000241420000",
                "46494E53000000160000000200000000C00002000D000001000201020000");
            TestAssert.True((await text).IsSuccess);
        }, InitializeFinsAsync);

    internal static Task FinsSessionErrorRetainsPartialWriteDetailsAsync() => WithPlcAsync(
        p => Plc.OmronFinsTcp("127.0.0.1", p, new FinsTcpClientOptions { MaxWordsPerRequest = 1 }), async (plc, peer) =>
        {
            var write = plc.WriteAsync("D0", new short[] { 1, 2 });
            await TcpFixture.ReadExactlyAsync(peer, 36);
            await TcpFixture.WriteAsync(peer, H("46494E53000000160000000200000000C00002000D000001000001020000"));
            await TcpFixture.ReadExactlyAsync(peer, 36);
            await TcpFixture.WriteAsync(peer, H("46494E53000000080000000300000021"));
            var result = await write;
            TestAssert.Equal(0x21, result.ErrorCode);
            TestAssert.Equal(1, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(1, result.FailureDetails.UncertainCount);
            TestAssert.True(result.FailureDetails.Cause != null);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        }, InitializeFinsAsync);
}
