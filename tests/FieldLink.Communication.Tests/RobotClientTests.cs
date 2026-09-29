using System.Text;
using FieldLink.Robot.EFORT.Clients;
using FieldLink.Robot.KUKA.Clients;
using FieldLink.Robot.YAMAHA.Clients;

namespace FieldLink.Communication.Tests;

internal static partial class RobotClientTests
{
    internal static async Task EfortHeartbeatAndPartialResponseAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new EfortTcpClient(pair.Client);
        for (ushort count = 0; count < 2; count++)
        {
            var read = robot.ReadAsync();
            byte[] command = await TcpFixture.ReadExactlyAsync(pair.Peer, 38);
            TestAssert.Equal("MessageHead", Encoding.ASCII.GetString(command, 0, 11));
            TestAssert.Equal((ushort)1001, BitConverter.ToUInt16(command, 18));
            TestAssert.Equal(count, BitConverter.ToUInt16(command, 20));
            byte[] response = new byte[20]; response[16] = 20; response[18] = 42;
            await TcpFixture.WriteAsync(pair.Peer, TestBytes.Slice(response, 0, 17));
            TestAssert.True(!read.IsCompleted);
            await TcpFixture.WriteAsync(pair.Peer, TestBytes.Slice(response, 17));
            TestAssert.Bytes(response, (await read).Content);
        }
    }

    internal static async Task KukaProxyReadsUsingDeclaredLengthAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new KukaVarProxyTcpClient(pair.Client);
        var read = robot.ReadStringAsync("A");
        TestAssert.Bytes(TestBytes.FromHexString("0000000400000141"), await TcpFixture.ReadExactlyAsync(pair.Peer, 8));
        // IMTS Answer Message Format has a three-byte success tail 00 01 01.
        await TcpFixture.WriteAsync(pair.Peer, TestBytes.FromHexString("00000009000003616263000101"));
        TestAssert.Equal("abc", (await read).Content);
    }

    internal static async Task YamahaReceivesResponseLinesAfterOneCommandAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var robot = new YamahaRcxTcpClient(pair.Client);
        var read = robot.ReadMotorStatusAsync();
        byte[] expected = Encoding.ASCII.GetBytes("@?MOTOR \r\n");
        TestAssert.Bytes(expected, await TcpFixture.ReadExactlyAsync(pair.Peer, expected.Length));
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("1\r\n"));
        // RCX340-PRO_E_V1.20 section 3.3: one query returns value then OK.
        TestAssert.True(!read.IsCompleted);
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("OK\r\n"));
        TestAssert.Equal(1, (await read).Content);
    }
}
