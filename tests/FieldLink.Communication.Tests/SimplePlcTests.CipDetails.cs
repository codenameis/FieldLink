using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.AllenBradley;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static Task CipPartialReadContinuesWithByteOffsetAsync() => WithPlcAsync(
        p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [] }), async (plc, peer) =>
        {
            var read = plc.ReadInt32Async("Tag", 3);
            var first = await ReadCipRequestAsync(peer);
            TestAssert.Bytes(H("4C039103546167000300"), first.Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(2, "CC000600C4000100000002000000"));
            var next = await ReadCipRequestAsync(peer);
            TestAssert.Bytes(H("5203910354616700030008000000"), next.Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(3, "D2000000C40003000000"));
            TestAssert.True((await read).Content.SequenceEqual(new[] { 1, 2, 3 }));
        }, InitializeCipAsync);

    internal static Task CipFragmentedWriteDoesNotReplayRejectedBlockAsync() => WithPlcAsync(
        p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [], MaxWriteBytesPerRequest = 8 }), async (plc, peer) =>
        {
            var write = plc.WriteAsync("Tag", new[] { 1, 2, 3 });
            TestAssert.Bytes(H("5303910354616700C4000300000000000100000002000000"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(2, "D3000000"));
            TestAssert.Bytes(H("5303910354616700C40003000800000003000000"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(3, "D300FF010521"));
            var result = await write;
            TestAssert.Equal(0xFF, result.ErrorCode);
            TestAssert.True(result.Message.Contains("05-21"));
            TestAssert.Equal(8, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(0, result.FailureDetails.UncertainCount);
            TestAssert.Equal(ClientState.Open, plc.State);
            TestAssert.Equal(0, peer.Available);
        }, InitializeCipAsync);

    internal static Task CipStringWritesDataBeforeLenAndReadsDeclaredLengthAsync() => WithPlcAsync(
        p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [] }), async (plc, peer) =>
        {
            var write = plc.WriteStringAsync("Text", "AB", 4);
            TestAssert.Bytes(H("4D079104546578749104444154412800C200040041420000"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(2, "CD000000"));
            TestAssert.Bytes(H("4D0691045465787491034C454E00C400010002000000"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(3, "CD000000"));
            TestAssert.True((await write).IsSuccess);
            var read = plc.ReadStringAsync("Text", 4);
            TestAssert.Bytes(H("4C0691045465787491034C454E000100"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(4, "CC000000C40002000000"));
            TestAssert.Bytes(H("4C0791045465787491044441544128000200"),
                (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(5, "CC000000C2004142"));
            TestAssert.Equal("AB", (await read).Content);
            var tooLong = plc.ReadStringAsync("Text", 4);
            await ReadCipRequestAsync(peer);
            await TcpFixture.WriteAsync(peer, CipReply(6, "CC000000C40005000000"));
            TestAssert.True(!(await tooLong).IsSuccess);
            TestAssert.Equal(0, peer.Available);
        }, InitializeCipAsync);

    internal static Task CipBoolAndTagValidationProtectTypeContractAsync() => WithPlcAsync(
        p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [] }), async (plc, peer) =>
        {
            TestAssert.True(!(await plc.ReadBoolAsync("Flags[0]", 32)).IsSuccess);
            TestAssert.True(!(await plc.WriteAsync("Tag[-1]", 1)).IsSuccess);
            TestAssert.True(!(await plc.WriteAsync(new string('A', 41), 1)).IsSuccess);
            TestAssert.Equal(0, peer.Available);
            var read = plc.ReadBoolAsync("Flag");
            TestAssert.Bytes(H("4C039104466C61670100"), (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(2, "CC000000C10001"));
            TestAssert.True((await read).Content);
            var write = plc.WriteAsync("Flag", false);
            TestAssert.Bytes(H("4D039104466C6167C100010000"), (await ReadCipRequestAsync(peer)).Skip(40).ToArray());
            await TcpFixture.WriteAsync(peer, CipReply(3, "CD000000"));
            TestAssert.True((await write).IsSuccess);
            var wrongType = plc.ReadInt32Async("Tag");
            await ReadCipRequestAsync(peer);
            await TcpFixture.WriteAsync(peer, CipReply(4, "CC000000CA000000803F"));
            TestAssert.True(!(await wrongType).IsSuccess);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        }, InitializeCipAsync);

    internal static Task CipInvalidPartialReadIsRejectedAsync() => WithPlcAsync(
        p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [] }), async (plc, peer) =>
        {
            var read = plc.ReadInt32Async("Tag", 2);
            await ReadCipRequestAsync(peer);
            await TcpFixture.WriteAsync(peer, CipReply(2, "CC000600C400"));
            TestAssert.True(!(await read).IsSuccess);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        }, InitializeCipAsync);
}
