using System.Net;
using System.Net.Sockets;
using FieldLink.PlcDrivers;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    private static async Task WithPlcAsync(Func<int, PlcClient> create, Func<PlcClient, Socket, Task> action,
        Func<Socket, Task>? initialize = null)
    {
        var listener = new TestTcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var plc = create(((IPEndPoint)listener.LocalEndpoint).Port);
            var accepted = listener.AcceptSocketAsync();
            var opening = plc.OpenAsync();
            using var peer = await accepted;
            if (initialize != null) await initialize(peer);
            TestAssert.True((await opening).IsSuccess);
            await action(plc, peer);
        }
        finally { listener.Stop(); }
    }

    private static async Task ExchangeExpectedAsync(Socket peer, string expected, string response)
    {
        byte[] bytes = H(expected);
        TestAssert.Bytes(bytes, await TcpFixture.ReadExactlyAsync(peer, bytes.Length));
        await TcpFixture.WriteAsync(peer, H(response));
    }

    private static async Task InitializeS7Async(Socket peer, int pdu = 480)
    {
        await ExchangeExpectedAsync(peer, "0300001611E00000000100C0010AC1020102C2020100", "0300001611D00001000100C0010AC1020102C2020100");
        await ExchangeExpectedAsync(peer, "0300001902F08032010000040000080000F0000001000101E0",
            "0300001B02F080320300000400000800000000F00000010001" + pdu.ToString("X4"));
    }

    private static Task InitializeFinsAsync(Socket peer) => ExchangeExpectedAsync(peer,
        "46494E530000000C000000000000000000000000", "46494E530000001000000001000000000000000D00000001");

    private static Task InitializeCipAsync(Socket peer) => ExchangeExpectedAsync(peer,
        "65000400000000000000000001000000000000000000000001000000", "65000400785634120000000001000000000000000000000001000000");

    // 독립 CPF fixture: 기존 golden vector의 헤더에 테스트별 CIP reply를 넣는다.
    private static byte[] CipReply(int context, string cip)
    {
        byte[] data = H(cip);
        byte[] frame = H("6F0000007856341200000000000000000000000000000000000000000000020000000000B2000000");
        Array.Resize(ref frame, 40 + data.Length);
        frame[2] = (byte)(frame.Length - 24); frame[3] = (byte)((frame.Length - 24) >> 8);
        frame[12] = (byte)context;
        frame[38] = (byte)data.Length; frame[39] = (byte)(data.Length >> 8);
        Array.Copy(data, 0, frame, 40, data.Length);
        return frame;
    }

    private static async Task<byte[]> ReadCipRequestAsync(Socket peer)
    {
        byte[] header = await TcpFixture.ReadExactlyAsync(peer, 24);
        byte[] body = await TcpFixture.ReadExactlyAsync(peer, header[2] + header[3] * 256);
        return header.Concat(body).ToArray();
    }
}
