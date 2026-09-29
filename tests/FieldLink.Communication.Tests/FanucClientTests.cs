using FieldLink.Robot.FANUC.Clients;
using FieldLink.Robot.FANUC.Protocols;
using System.Net.Sockets;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task FanucInitializesEachConnectionAndReadsWordsAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new FanucTcpClient(pair.Client);
        for (int generation = 0; generation < 2; generation++)
        {
            Socket peer = pair.Peer;
            if (generation != 0)
            {
                pair.Client.Close();
                await pair.Client.OpenAsync();
                peer = await pair.Listener.AcceptSocketAsync();
            }

            try
            {
                var read = robot.ReadAsync("D100", 2);
                for (int i = 0; i < 62; i++)
                {
                    byte[] header = await TcpFixture.ReadExactlyAsync(peer, 56);
                    int length = BitConverter.ToUInt16(header, 4);
                    byte[] body = await TcpFixture.ReadExactlyAsync(peer, length);
                    if (i == 0)
                        TestAssert.Equal(1024, BitConverter.ToInt32(header, 1));
                    // 6바이트 CLRASG는 긴 본문이 아니라 짧은 쓰기 헤더의 48번 위치에 들어간다.
                    if (i == 2)
                        TestAssert.Equal("CLRASG", System.Text.Encoding.ASCII.GetString(header, 48, 6));
                    await TcpFixture.WriteAsync(peer, i == 0 ? FanucServerProtocol.BuildConnectReply() : FanucServerProtocol.BuildWriteReply(header[2]));
                }

                byte[] request = await TcpFixture.ReadExactlyAsync(peer, 56);
                TestAssert.Equal((ushort)99, BitConverter.ToUInt16(request, 44));
                TestAssert.Equal((ushort)2, BitConverter.ToUInt16(request, 46));
                await TcpFixture.WriteAsync(peer, FanucProtocol.BuildReadResponseData([10, 0, 20, 0]));
                TestAssert.Bytes([10, 0, 20, 0], (await read).Content);
            }
            finally
            {
                if (generation != 0)
                    peer.Dispose();
            }
        }
    }
}
