using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Protocols.Yamaha.Rcx;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Yamaha.Rcx;

public sealed class YamahaRcxTests
{
    [Fact]
    public void RequestBuilder_SpecVectorsAndAsciiLoad_MatchExactBytes()
    {
        // 출처: docs/specs/robot/07-yamaha-rcx.md §9.2
        Assert.Equal(
            FakeTransport.ParseHex("40 3F 4D 4F 54 4F 52 20 0D 0A"),
            YamahaRcxRequestBuilder.BuildReadMotorStatus());

        Assert.Equal(
            FakeTransport.ParseHex("40 3F 57 48 45 52 45 20 0D 0A"),
            YamahaRcxRequestBuilder.BuildReadJoints());

        Assert.Equal(
            "@ JOGXY 2-\r\n",
            Encoding.ASCII.GetString(YamahaRcxRequestBuilder.BuildJogXY(axis: -2, robot: 1)));

        Assert.Equal(
            "@ JOGXY [2] 1+\r\n",
            Encoding.ASCII.GetString(YamahaRcxRequestBuilder.BuildJogXY(axis: 1, robot: 2)));

        byte[] load = YamahaRcxRequestBuilder.BuildLoad("MAIN", 1);
        Assert.Equal(0x40, load[0]); // 표준 ASCII '@' 검증 (전각 '＠' 금지)
        Assert.Equal("@ LOAD <MAIN>, T1\r\n", Encoding.ASCII.GetString(load));
    }

    [Fact]
    public async Task Client_ReadMotorStatusAndJoints_SendOnceAndReceiveExpectedLines()
    {
        var transport = new FakeTransport();
        // 출처: docs/specs/robot/07-yamaha-rcx.md §9.2 ReadMotorStatus & ReadJoints
        transport.EnqueueIncomingHex("32 0D 0A 4F 4B 0D 0A"); // "2\r\nOK\r\n" 단일 수신 버퍼로 도착
        transport.EnqueueIncomingAscii("12.50 -45.25 100.00 0.00\r\n");

        using var client = new YamahaRcxClient(transport);
        await client.OpenAsync();

        RobotResult<YamahaRcxMotorStatus> motor = await client.ReadMotorStatusAsync();
        Assert.True(motor.IsSuccess);
        Assert.Equal(YamahaRcxMotorStatus.OnAllServosEnabled, motor.Value);
        Assert.Single(transport.SentFrames); // 2줄 응답이라도 명령은 1회만 송신!

        RobotResult<float[]> joints = await client.ReadJointsAsync();
        Assert.True(joints.IsSuccess);
        Assert.Equal(new[] { 12.5f, -45.25f, 100.0f, 0.0f }, joints.Value);
        Assert.Equal(2, transport.SentFrames.Count);
    }

    [Fact]
    public async Task Client_EarlyNgErrorOnMultiLineCommand_StopsReadingBeforeSecondLine()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingAscii("NG 2101 Syntax error\r\n");

        using var client = new YamahaRcxClient(transport);
        await client.OpenAsync();

        RobotResult<YamahaRcxMotorStatus> result = await client.ReadMotorStatusAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, result.Error.Kind);
        Assert.Equal("NG 2101 Syntax error", result.Error.Message);
    }
}
