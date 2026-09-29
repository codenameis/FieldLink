using System.Net;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task FinsOpenNegotiatesAndTypedCallsReuseSessionAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.OmronFinsTcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var opening = plc.OpenAsync();
            using var peer = await listener.AcceptSocketAsync();
            TestAssert.Bytes(H("46494E530000000C000000000000000000000000"), await TcpFixture.ReadExactlyAsync(peer, 20));
            await TcpFixture.WriteAsync(peer, H("46494E530000001000000001000000000000000D00000001"));
            TestAssert.True((await opening).IsSuccess);
            var read = plc.ReadInt32Async("D100");
            TestAssert.Bytes(H("46494E530000001A0000000200000000800002000100000D00000101820064000002"),
                await TcpFixture.ReadExactlyAsync(peer, 34));
            await TcpFixture.WriteAsync(peer, H("46494E530000001A0000000200000000C00002000D0000010000010100001234ABCD"));
            TestAssert.Equal(unchecked((int)0xABCD1234), (await read).Content);
            var write = plc.WriteAsync("D100", 123);
            TestAssert.Bytes(H("46494E530000001E0000000200000000800002000100000D00010102820064000002007B0000"),
                await TcpFixture.ReadExactlyAsync(peer, 38));
            await TcpFixture.WriteAsync(peer, H("46494E53000000160000000200000000C00002000D000001000101020000"));
            TestAssert.True((await write).IsSuccess);
        }
        finally { listener.Stop(); }
    }

    internal static async Task S7OpenNegotiatesAndTypedReadWriteSharePduSequenceAsync()
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = Plc.SiemensS7Tcp("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            var opening = plc.OpenAsync();
            using var peer = await listener.AcceptSocketAsync();
            TestAssert.Bytes(H("0300001611E00000000100C0010AC1020102C2020100"), await TcpFixture.ReadExactlyAsync(peer, 22));
            await TcpFixture.WriteAsync(peer, H("0300001611D00001000100C0010AC1020102C2020100"));
            TestAssert.Bytes(H("0300001902F08032010000040000080000F0000001000101E0"), await TcpFixture.ReadExactlyAsync(peer, 25));
            await TcpFixture.WriteAsync(peer, H("0300001B02F080320300000400000800000000F0000001000101E0"));
            TestAssert.True((await opening).IsSuccess);
            var read = plc.ReadInt32Async("DB1.0");
            TestAssert.Bytes(H("0300001F02F080320100000001000E00000401120A10020004000184000000"), await TcpFixture.ReadExactlyAsync(peer, 31));
            await TcpFixture.WriteAsync(peer, H("0300001D02F0803203000000010002000800000401FF0400201234ABCD"));
            TestAssert.Equal(0x1234ABCD, (await read).Content);
            var write = plc.WriteAsync("DB1.4", 123);
            TestAssert.Bytes(H("0300002702F080320100000002000E00080501120A10020004000184000020000400200000007B"),
                await TcpFixture.ReadExactlyAsync(peer, 39));
            await TcpFixture.WriteAsync(peer, H("0300001602F0803203000000020002000100000501FF"));
            TestAssert.True((await write).IsSuccess);
        }
        finally { listener.Stop(); }
    }
}
