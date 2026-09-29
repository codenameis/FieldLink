using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static Task CommonNumericOverloadsPreserveSignednessAndWidthAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p), async (plc, peer) =>
        {
            int id = 0;
            async Task CheckWrite(Func<Task<FieldLink.PlcDrivers.Common.OperationResult>> send, string payload)
            {
                byte[] bytes = H(payload);
                int words = bytes.Length / 2;
                var pending = send();
                id++;
                await ExchangeExpectedAsync(peer, id.ToString("X4") + "0000" + (7 + bytes.Length).ToString("X4") +
                    "01100000" + words.ToString("X4") + bytes.Length.ToString("X2") + payload,
                    id.ToString("X4") + "0000000601100000" + words.ToString("X4"));
                TestAssert.True((await pending).IsSuccess);
            }
            await CheckWrite(() => plc.WriteAsync("0", new short[] { -2, 3 }), "FFFE0003");
            await CheckWrite(() => plc.WriteAsync("0", new ushort[] { 65535, 3 }), "FFFF0003");
            await CheckWrite(() => plc.WriteAsync("0", new[] { -2, 3 }), "FFFFFFFE00000003");
            await CheckWrite(() => plc.WriteAsync("0", new[] { uint.MaxValue, 3u }), "FFFFFFFF00000003");
            await CheckWrite(() => plc.WriteAsync("0", new[] { -2L, 3L }), "FFFFFFFFFFFFFFFE0000000000000003");
            await CheckWrite(() => plc.WriteAsync("0", new[] { ulong.MaxValue, 3UL }), "FFFFFFFFFFFFFFFF0000000000000003");
            await CheckWrite(() => plc.WriteAsync("0", new[] { 1.5f, -2f }), "3FC00000C0000000");
            await CheckWrite(() => plc.WriteAsync("0", new[] { 1.5, -2.0 }), "3FF8000000000000C000000000000000");
            var floating = plc.ReadDoubleAsync("0", 2);
            await ExchangeExpectedAsync(peer, "000900000006010300000008", "0009000000130103103FF8000000000000C000000000000000");
            TestAssert.True((await floating).Content.SequenceEqual(new[] { 1.5, -2.0 }));
            var unsigned = plc.ReadUInt64Async("0");
            await ExchangeExpectedAsync(peer, "000A00000006010300000004", "000A0000000B010308FFFFFFFFFFFFFFFF");
            TestAssert.Equal(ulong.MaxValue, (await unsigned).Content);
        });
}
