using System.Net;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task LsTypedReadAndArrayWriteUseByteAddressesAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.LsFastEnetTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            TestAssert.True((await plc.OpenAsync()).IsSuccess);
            using var peer = await accepted;
            var read = plc.ReadInt32Async("D100");
            TestAssert.Bytes(H("4C5349532D58475400000000A033010012000344540014000000010006002544423230300400"),
                await TcpFixture.ReadExactlyAsync(peer, 38));
            await TcpFixture.WriteAsync(peer, H("4C5349532D58475400000101A01101001000001F5500140000000000010004003412CDAB"));
            TestAssert.Equal(unchecked((int)0xABCD1234), (await read).Content);
            var write = plc.WriteAsync("D102", new[] { 10, 20 });
            TestAssert.Bytes(H("4C5349532D58475400000000A03302001A00034D5800140000000100060025444232303408000A00000014000000"),
                await TcpFixture.ReadExactlyAsync(peer, 46));
            await TcpFixture.WriteAsync(peer, H("4C5349532D58475400000101A01102000A00001A59001400000000000100"));
            TestAssert.True((await write).IsSuccess);
        }
        finally { listener.Stop(); }
    }
}
