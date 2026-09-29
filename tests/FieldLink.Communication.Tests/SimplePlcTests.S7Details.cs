using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static Task S7StringPreservesCapacityAndPadsDataAsync() => WithPlcAsync(
        p => Plc.SiemensS7Tcp("127.0.0.1", p), async (plc, peer) =>
        {
            var write = plc.WriteStringAsync("DB1.0", "AB", 4);
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000001000E00000401120A10020002000184000000",
                "0300001B02F0803203000000010002000600000401FF0400100401");
            await ExchangeExpectedAsync(peer, "0300002902F080320100000002000E000A0501120A1002000600018400000000040030040241420000",
                "0300001602F0803203000000020002000100000501FF");
            TestAssert.True((await write).IsSuccess);
            var read = plc.ReadStringAsync("DB1.0", 4);
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000003000E00000401120A10020002000184000000",
                "0300001B02F0803203000000030002000600000401FF0400100402");
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000004000E00000401120A10020002000184000010",
                "0300001B02F0803203000000040002000600000401FF0400104142");
            TestAssert.Equal("AB", (await read).Content);
            var tooLarge = plc.WriteStringAsync("DB1.0", "ABC", 6);
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000005000E00000401120A10020002000184000000",
                "0300001B02F0803203000000050002000600000401FF0400100402");
            TestAssert.True(!(await tooLarge).IsSuccess);
            TestAssert.Equal(0, peer.Available);
        }, peer => InitializeS7Async(peer));

    internal static Task S7NegotiatedPduSplitsArrayAndKeepsDeviceErrorAsync() => WithPlcAsync(
        p => Plc.SiemensS7Tcp("127.0.0.1", p), async (plc, peer) =>
        {
            // PDU=36: S7 단일 쓰기 헤더 28바이트 + 데이터 8바이트.
            var write = plc.WriteAsync("DB1.0", new[] { 1, 2, 3 });
            await ExchangeExpectedAsync(peer, "0300002B02F080320100000001000E000C0501120A10020008000184000000000400400000000100000002",
                "0300001602F0803203000000010002000100000501FF");
            await ExchangeExpectedAsync(peer, "0300002702F080320100000002000E00080501120A100200040001840000400004002000000003",
                "0300001602F08032030000000200020001000005010A");
            var result = await write;
            TestAssert.Equal(10, result.ErrorCode);
            TestAssert.Equal(8, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(0, result.FailureDetails.UncertainCount);
            TestAssert.Equal(ClientState.Open, plc.State);
        }, peer => InitializeS7Async(peer, 36));

    internal static Task S7BitAddressCarriesIntoNextByteAsync() => WithPlcAsync(
        p => Plc.SiemensS7Tcp("127.0.0.1", p), async (plc, peer) =>
        {
            var bits = plc.ReadBoolAsync("M0.7", 2);
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000001000E00000401120A10010001000083000007",
                "0300001A02F0803203000000010002000500000401FF03000101");
            await ExchangeExpectedAsync(peer, "0300001F02F080320100000002000E00000401120A10010001000083000008",
                "0300001A02F0803203000000020002000500000401FF03000100");
            TestAssert.True((await bits).Content.SequenceEqual(new[] { true, false }));
            TestAssert.True(!(await plc.WriteAsync("DB1.1.1", 123)).IsSuccess);
            TestAssert.True(!(await plc.ReadInt16Async("C0")).IsSuccess);
        }, peer => InitializeS7Async(peer));
}
