using System.Net;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task CipOpenReadArrayWriteAndCloseHideSessionDetailsAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.AllenBradleyTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            var opening = plc.OpenAsync();
            using var peer = await accepted;
            TestAssert.Bytes(H("65000400000000000000000001000000000000000000000001000000"), await TcpFixture.ReadExactlyAsync(peer, 28));
            await TcpFixture.WriteAsync(peer, H("65000400785634120000000001000000000000000000000001000000"));
            TestAssert.True((await opening).IsSuccess);
            var read = plc.ReadInt32Async("Tag");
            TestAssert.Bytes(H("6F0028007856341200000000020000000000000000000000" +
                "000000000A00020000000000B20018005202200624010AF00A004C03910354616700010001000100"),
                await TcpFixture.ReadExactlyAsync(peer, 64));
            await TcpFixture.WriteAsync(peer, H("6F001A007856341200000000020000000000000000000000000000000000020000000000B2000A00CC000000C4003412CDAB"));
            TestAssert.Equal(unchecked((int)0xABCD1234), (await read).Content);
            var write = plc.WriteAsync("Tag", new[] { 10, 20 });
            TestAssert.Bytes(H("6F0032007856341200000000030000000000000000000000" +
                "000000000A00020000000000B20022005202200624010AF014004D03910354616700C40002000A0000001400000001000100"),
                await TcpFixture.ReadExactlyAsync(peer, 74));
            await TcpFixture.WriteAsync(peer, H("6F0014007856341200000000030000000000000000000000000000000000020000000000B2000400CD000000"));
            TestAssert.True((await write).IsSuccess);
            var closing = plc.CloseAsync();
            TestAssert.Bytes(H("660000007856341200000000000000000000000000000000"), await TcpFixture.ReadExactlyAsync(peer, 24));
            TestAssert.True((await closing).IsSuccess);
        }
        finally { listener.Stop(); }
    }
}
