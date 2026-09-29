using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.LSIS;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static Task LsBitsUseDecimalBitOffsetsAndIndividualReadsAsync() => WithPlcAsync(
        p => Plc.LsFastEnetTcp("127.0.0.1", p), async (plc, peer) =>
        {
            // LS manual §8.2.3: M172.C의 wire 주소는 %MX2764이며 %MX172C가 아니다.
            var read = plc.ReadBoolAsync("MX2764", 2);
            await ExchangeExpectedAsync(peer, "4C5349532D58475400000000A03301001100034354000000000001000700254D5832373634",
                "4C5349532D58475400000101A01101000D00000055000000000000000100010001");
            await ExchangeExpectedAsync(peer, "4C5349532D58475400000000A03302001100034454000000000001000700254D5832373635",
                "4C5349532D58475400000101A01102000D00000055000000000000000100010000");
            TestAssert.True((await read).Content.SequenceEqual(new[] { true, false }));
            var write = plc.WriteAsync("MX2764", true);
            await ExchangeExpectedAsync(peer, "4C5349532D58475400000000A03303001400034858000000000001000700254D5832373634010001",
                "4C5349532D58475400000101A01103000A00000059000000000000000100");
            TestAssert.True((await write).IsSuccess);
        });

    internal static Task LsByteSplitAndDeviceRejectionRetainProgressAsync() => WithPlcAsync(
        p => Plc.LsFastEnetTcp("127.0.0.1", p, new LsFastEnetClientOptions { MaxBytesPerRequest = 4 }), async (plc, peer) =>
        {
            var write = plc.WriteAsync("D100", new[] { 1, 2, 3 });
            await ExchangeExpectedAsync(peer, "4C5349532D58475400000000A03301001600034858001400000001000600254442323030040001000000",
                "4C5349532D58475400000101A01101000A00000059001400000000000100");
            await ExchangeExpectedAsync(peer, "4C5349532D58475400000000A03302001600034958001400000001000600254442323034040002000000",
                "4C5349532D58475400000101A011020009000000590014000000FFFF21");
            var result = await write;
            TestAssert.Equal(0x21, result.ErrorCode);
            TestAssert.Equal(4, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(0, result.FailureDetails.UncertainCount);
            TestAssert.Equal(ClientState.Open, plc.State);
        });
}
