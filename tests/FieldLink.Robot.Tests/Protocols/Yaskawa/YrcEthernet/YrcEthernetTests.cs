using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Yaskawa.YrcEthernet;

public sealed class YrcEthernetTests
{
    [Fact]
    public async Task Client_SpecVectorRstatsAndConnectHandshake_ParseBitsCorrectly()
    {
        var transport = new FakeTransport();
        // 출처: docs/specs/robot/08-yaskawa.md §8.3 Vector 1 (RSTATS)
        transport.EnqueueIncomingAscii("OK:YR Information Server(Ver) Keep-Alive:-1.\r\n");
        transport.EnqueueIncomingAscii("OK: RSTATS\r\n");
        transport.EnqueueIncomingAscii("194,64\r");

        using var client = new YrcEthernetClient(transport);
        await client.OpenAsync();

        RobotResult<bool[]> stats = await client.ReadStatusBitsAsync();
        Assert.True(stats.IsSuccess);
        Assert.True(client.IsPersistentConnection);
        Assert.Equal("CONNECT Robot_access KeepAlive:-1\r\n", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal("HOSTCTRL_REQUEST RSTATS 0\r\n", Encoding.ASCII.GetString(transport.SentFrames[1]));

        // 194 = 0b11000010 (Bit 1, 6, 7), 64 = 0b01000000 (Bit 14)
        Assert.True(stats.Value[1]);
        Assert.True(stats.Value[6]);
        Assert.True(stats.Value[7]);
        Assert.True(stats.Value[14]);
        Assert.False(stats.Value[0]);
    }

    [Fact]
    public async Task Client_Phase2CrOnlyAndPhase3NewlineDrain_AllowsBackToBackCommands()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingAscii("OK:YR Information Server(Ver) Keep-Alive:-1.\r\n");
        // Command 1: HOLD=1 -> 0000\r\n
        transport.EnqueueIncomingAscii("OK: HOLD\r\n");
        transport.EnqueueIncomingAscii("0000\r\n");
        // Command 2: START -> ERROR (2010)\r\n
        transport.EnqueueIncomingAscii("OK: START\r\n");
        transport.EnqueueIncomingAscii("ERROR: Robot is moving (2010).\r\n");
        // Command 3: RALARM after draining \n cleanly!
        transport.EnqueueIncomingAscii("OK: RALARM\r\n");
        transport.EnqueueIncomingAscii("NO_ALARM\r");

        using var client = new YrcEthernetClient(transport);
        await client.OpenAsync();

        RobotResult hold = await client.HoldAsync(true);
        Assert.True(hold.IsSuccess);
        Assert.Equal("HOSTCTRL_REQUEST HOLD 2\r\n", Encoding.ASCII.GetString(transport.SentFrames[1]));
        Assert.Equal(new byte[] { (byte)'1', 0x0D }, transport.SentFrames[2]); // \n 없이 \r만 전송

        RobotResult start = await client.StartJobAsync();
        Assert.False(start.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, start.Error.Kind);
        Assert.Equal(2010, start.Error.DeviceErrorCode);

        RobotResult<string> alarm = await client.ReadAlarmAsync();
        Assert.True(alarm.IsSuccess);
        Assert.Equal("NO_ALARM", alarm.Value);
    }

    [Fact]
    public async Task Client_NonPersistentConnect_MarksNonPersistentAndClosesTransport()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingAscii("OK:YR Server ShortSession.\r\n");
        transport.EnqueueIncomingAscii("OK: RALARM\r\n");
        transport.EnqueueIncomingAscii("NONE\r");

        using var client = new YrcEthernetClient(transport);
        await client.OpenAsync();

        RobotResult<string> alarm = await client.ReadAlarmAsync();
        Assert.True(alarm.IsSuccess);
        Assert.False(client.IsPersistentConnection);
        Assert.Equal(1, transport.CloseCount);
    }

    [Fact]
    public void PoseAndValidation_Yrc1000VsYrc100AndMdspLimit_WorkCorrectly()
    {
        YrcRobotPose pose6 = YrcRobotPose.Parse("100.123,-50.5,200,0,90,-180,5,1", YrcModelType.Yrc1000);
        Assert.Equal(100.123, pose6.X, 3);
        Assert.Equal(5, pose6.StatusByte);
        Assert.Equal(1, pose6.ToolNumber);
        Assert.Contains("100.0%,0,100.123,-50.500,200.000,0.0000,90.0000,-180.0000,5,1,", pose6.ToMoveCommandString(YrcModelType.Yrc1000), StringComparison.Ordinal);

        YrcRobotPose pose7 = YrcRobotPose.Parse("10,20,30,1,2,3,45.5,2,3", YrcModelType.Yrc100);
        Assert.Equal(45.5, pose7.Re, 3);
        Assert.Equal(2, pose7.StatusByte);
        Assert.Equal(3, pose7.ToolNumber);

        Assert.Throws<ArgumentOutOfRangeException>(() => YrcEthernetRequestBuilder.ValidateDisplayMessage(new string('A', 31), Encoding.ASCII));
        Assert.Throws<ArgumentOutOfRangeException>(() => YrcEthernetRequestBuilder.BuildIoReadData(1, 5));
    }
}
