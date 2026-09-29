using System.Net.Sockets;
using System.Text;
using FieldLink.Communication.Diagnostics;
using FieldLink.Robot.YASKAWA.Clients;
using FieldLink.Robot.YASKAWA.Protocols;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task YrcNegotiatesAgainAfterReconnectAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new YrcTcpClient(pair.Client);
        for (int connection = 0; connection < 2; connection++)
        {
            Socket peer = pair.Peer;
            if (connection == 1)
            {
                pair.Client.Close();
                await pair.Client.OpenAsync();
                peer = await pair.Listener.AcceptSocketAsync();
            }

            try
            {
                var read = robot.ExecuteAsync(YrcTcpRequestBuilder.BuildReadAlarm());
                await ExpectText(peer, "CONNECT Robot_access KeepAlive:-1\r\n");
                await TcpFixture.WriteAsync(peer, Encoding.ASCII.GetBytes("OK:YR Information Server(Ver) Keep-Alive:-1.\r\n"));
                await ExpectText(peer, "HOSTCTRL_REQUEST RALARM 0\r\n");
                await TcpFixture.WriteAsync(peer, Encoding.ASCII.GetBytes("OK:RALARM\r\nAlarm\r"));
                TestAssert.Equal("Alarm", (await read).Content);
                var reset = robot.ExecuteAsync(YrcTcpRequestBuilder.BuildReset());
                await ExpectText(peer, "HOSTCTRL_REQUEST RESET 0\r\n");
                await TcpFixture.WriteAsync(peer, Encoding.ASCII.GetBytes("OK:RESET\r\n0000\r\n"));
                TestAssert.Equal("0000", (await reset).Content);
            }
            finally
            {
                if (connection == 1)
                    peer.Dispose();
            }
        }
    }

    internal static async Task YrcOneShotConnectionClosesAfterSuccessfulReplyAsync()
    {
        using var pair = await TcpFixture.CreateAsync(); var robot = new YrcTcpClient(pair.Client);
        var read = robot.ExecuteAsync(YrcTcpRequestBuilder.BuildReadAlarm());
        await ExpectText(pair.Peer, "CONNECT Robot_access KeepAlive:-1\r\n");
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("OK:YR Information Server(Ver).\r\n"));
        await ExpectText(pair.Peer, "HOSTCTRL_REQUEST RALARM 0\r\n");
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("OK:RALARM\r\nAlarm\r"));
        TestAssert.True((await read).IsSuccess);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => robot.ExecuteAsync(YrcTcpRequestBuilder.BuildReset()));
    }

    private static async Task ExpectText(Socket peer, string expected) =>
        TestAssert.Bytes(Encoding.ASCII.GetBytes(expected), await TcpFixture.ReadExactlyAsync(peer, Encoding.ASCII.GetByteCount(expected)));
}
