using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.AllenBradley;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task ExplicitReconnectRecreatesProtocolSessionsAsync()
    {
        var cases = new (Func<int, PlcClient> Create, Func<Socket, Task> Initialize, string Address, string Request, string Reply)[]
        {
            (p => Plc.SiemensS7Tcp("127.0.0.1", p), peer => InitializeS7Async(peer), "DB1.0",
                "0300001F02F080320100000001000E00000401120A10020004000184000000",
                "0300001D02F0803203000000010002000800000401FF04002000000007"),
            (p => Plc.OmronFinsTcp("127.0.0.1", p), InitializeFinsAsync, "D0",
                "46494E530000001A0000000200000000800002000100000D00000101820000000002",
                "46494E530000001A0000000200000000C00002000D00000100000101000000070000"),
            (p => Plc.AllenBradleyTcp("127.0.0.1", p, new AllenBradleyTcpClientOptions { RoutePath = [] }), InitializeCipAsync, "Tag",
                "6F001A007856341200000000020000000000000000000000000000000A00020000000000B2000A004C039103546167000100",
                "6F001A007856341200000000020000000000000000000000000000000000020000000000B2000A00CC000000C40007000000")
        };
        foreach (var item in cases)
        {
            var listener = new TestTcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                using var plc = item.Create(((IPEndPoint)listener.LocalEndpoint).Port);
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    var accepted = listener.AcceptSocketAsync();
                    var opening = plc.OpenAsync();
                    using var peer = await accepted;
                    await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadInt32Async(item.Address));
                    await item.Initialize(peer);
                    TestAssert.True((await opening).IsSuccess);
                    var read = plc.ReadInt32Async(item.Address);
                    await ExchangeExpectedAsync(peer, item.Request, item.Reply);
                    TestAssert.Equal(7, (await read).Content);
                    plc.Close();
                    await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadInt32Async(item.Address));
                }
            }
            finally { listener.Stop(); }
        }
    }

    internal static async Task RejectedOpenReturnsDeviceCodeAndCannotReadAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.AllenBradleyTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var opening = plc.OpenAsync();
            using var peer = await listener.AcceptSocketAsync();
            await ExchangeExpectedAsync(peer, "65000400000000000000000001000000000000000000000001000000",
                "650000000000000069000000010000000000000000000000");
            var result = await opening;
            TestAssert.Equal(0x69, result.ErrorCode);
            TestAssert.True(result.FailureDetails.Cause != null);
            TestAssert.Equal(ClientState.Faulted, plc.State);
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => plc.ReadInt32Async("Tag"));
        }
        finally { listener.Stop(); }
    }

    internal static Task FinsTruncatedSuccessfulReadFaultsConnectionAsync() => WithPlcAsync(
        p => Plc.OmronFinsTcp("127.0.0.1", p), async (plc, peer) =>
        {
            var read = plc.ReadInt32Async("D0");
            await TcpFixture.ReadExactlyAsync(peer, 34);
            await TcpFixture.WriteAsync(peer, H("46494E53000000160000000200000000C00002000D000001000001010000"));
            TestAssert.True(!(await read).IsSuccess);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        }, InitializeFinsAsync);
}
