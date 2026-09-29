using System.Net;
using System.Text;
using FieldLink.Communication.Framing;
using FieldLink.Robot.KUKA.Clients;
using FieldLink.Robot.YASKAWA.Clients;
using FieldLink.Robot.YASKAWA.Protocols;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task KukaTextCommandsRespectConfiguredBoundaryAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new KukaTextTcpClient(pair.Client, new DelimitedFrame([13, 10]));
        var read = robot.ReadStringAsync("A");
        TestAssert.Bytes(Encoding.UTF8.GetBytes("00A"), await TcpFixture.ReadExactlyAsync(pair.Peer, 3));
        await TcpFixture.WriteAsync(pair.Peer, Encoding.UTF8.GetBytes("42\r"));
        TestAssert.True(!read.IsCompleted);
        await TcpFixture.WriteAsync(pair.Peer, [10]);
        TestAssert.Equal("42\r\n", (await read).Content);
        var stop = robot.StopProgramAsync();
        TestAssert.Bytes(Encoding.UTF8.GetBytes("0621"), await TcpFixture.ReadExactlyAsync(pair.Peer, 4));
        await TcpFixture.WriteAsync(pair.Peer, Encoding.UTF8.GetBytes("err stopped\r\n"));
        TestAssert.True(!(await stop).IsSuccess);
    }
    internal static async Task YrcUdpMatchesRequestIdAndPropagatesDeviceErrorAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var transport = new FieldLink.Communication.Udp.UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await transport.OpenAsync();
        var robot = new YrcHighEthernetUdpClient(transport);
        var builder = new YrcEthernetRequestBuilder();
        var read = robot.ExecuteAsync(builder.BuildReadByteVariable(10));
        var request = await peer.ReceiveAsync();
        TestAssert.Bytes(TestBytes.FromHexString("5945524320000000030100000000000039393939393939397A000A00010E0000"), request.Buffer);
        // 공식 HSES 응답 헤더: 32바이트 헤더, 데이터 1바이트, 응답 ACK, 서비스 0x8E.
        byte[] response = TestBytes.FromHexString("5945524320000100030101630000000039393939393939398E000000000000004D");
        await peer.SendAsync(response, request.RemoteEndPoint);
        response[11] = request.Buffer[11]; response[32] = 42;
        await peer.SendAsync(response, request.RemoteEndPoint);
        TestAssert.Equal((byte)42, (await read).Content);
        var next = robot.ExecuteAsync(builder.BuildReadByteVariable(10));
        request = await peer.ReceiveAsync(); TestAssert.Equal((byte)1, request.Buffer[11]);
        response[11] = 1; response[25] = 8;
        await peer.SendAsync(response, request.RemoteEndPoint);
        var error = await next; TestAssert.True(!error.IsSuccess); TestAssert.Equal(8, error.ErrorCode);
    }
}
