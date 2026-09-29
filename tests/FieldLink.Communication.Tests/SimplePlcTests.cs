using System.Net;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task ModbusTypedReadAndArrayWriteUseOneClientAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.ModbusTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            TestAssert.True((await plc.OpenAsync()).IsSuccess);
            using var peer = await accepted;
            var read = plc.ReadInt32Async("0");
            TestAssert.Bytes(H("000100000006010300000002"), await TcpFixture.ReadExactlyAsync(peer, 12));
            await TcpFixture.WriteAsync(peer, H("00010000000701030412345678"));
            TestAssert.Equal(0x12345678, (await read).Content);
            var write = plc.WriteAsync("10", new[] { 10, 20 });
            TestAssert.Bytes(H("00020000000F0110000A0004080000000A00000014"), await TcpFixture.ReadExactlyAsync(peer, 21));
            await TcpFixture.WriteAsync(peer, H("0002000000060110000A0004"));
            TestAssert.True((await write).IsSuccess);
        }
        finally { listener.Stop(); }
    }

    internal static async Task KeyenceMcFactoryAcceptsNativeDmAddressAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.KeyenceMcTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            TestAssert.True((await plc.OpenAsync()).IsSuccess);
            using var peer = await accepted;
            var write = plc.WriteAsync("DM100", 123);
            TestAssert.Bytes(H("500000FFFF030010000A0001140000640000A802007B000000"),
                await TcpFixture.ReadExactlyAsync(peer, 25));
            await TcpFixture.WriteAsync(peer, H("D00000FFFF030002000000"));
            TestAssert.True((await write).IsSuccess);
        }
        finally { listener.Stop(); }
    }

    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
}
