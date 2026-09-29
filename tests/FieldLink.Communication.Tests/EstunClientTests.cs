using FieldLink.PlcDrivers.Modbus;
using FieldLink.Robot.Estun.Clients;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;
internal static partial class RobotClientTests
{
    internal static async Task EstunExecutesReferenceRegisterSequenceAsync()
    {
        using var pair = await TcpFixture.CreateAsync(); var robot = new EstunTcpClient(pair.Client);
        var start = robot.StartProgramAsync();
        string[] expected = ["010300630001", "010300330001", "010600630011", "010300120001", "010600330004", "010300120001", "010600630000", "010600330000"];
        for (int i = 0; i < expected.Length; i++)
        {
            var header = await TcpFixture.ReadExactlyAsync(pair.Peer, 6);
            var core = await TcpFixture.ReadExactlyAsync(pair.Peer, header[4] * 256 + header[5]);
            TestAssert.Bytes(TestBytes.FromHexString(expected[i]), core);
            byte[] response = core[1] == 3 ? new byte[] { 1, 3, 2, i == 3 ? (byte)8 : (byte)0, i == 3 ? (byte)1 : (byte)0 } : core;
            await TcpFixture.WriteAsync(pair.Peer, ModbusFrameRules.PackCommandToTcp(response, (ushort)(header[0] * 256 + header[1])));
        }
        TestAssert.True((await start).IsSuccess);
    }
}
