using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Protocols.Kuka.VarProxy;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Kuka.VarProxy;

public sealed class KukaVarProxyTests
{
    [Fact]
    public void RequestBuilderAndResponseParser_SpecVectors_MatchExactBytesAndValues()
    {
        // 출처: docs/specs/robot/06-kuka.md §9.2 Vector A1 ($OV_PRO read, MsgID=1)
        byte[] expectedReq = FakeTransport.ParseHex("00 01 00 0A 00 00 07 24 4F 56 5F 50 52 4F");
        byte[] actualReq = KukaVarProxyRequestBuilder.BuildReadRequest(1, "$OV_PRO");
        Assert.Equal(expectedReq, actualReq);

        // 출처: docs/specs/robot/06-kuka.md §9.2 Vector A2 ($OV_PRO read response = "50")
        byte[] response = FakeTransport.ParseHex("00 01 00 08 00 00 02 35 30 00 00 01");
        byte[] parsed = KukaVarProxyResponseParser.ParseValueBytes(response, 1, KukaVarProxyFunctionCode.Read);
        Assert.Equal("50", Encoding.ASCII.GetString(parsed));
    }

    [Fact]
    public void ResponseParser_StatusErrorMsgIdMismatchAndShortFrame_ThrowAppropriateExceptions()
    {
        byte[] failedStatus = FakeTransport.ParseHex("00 01 00 06 00 00 00 00 00 00");
        RobotDeviceException deviceError = Assert.Throws<RobotDeviceException>(
            () => KukaVarProxyResponseParser.ParseValueBytes(failedStatus, 1, KukaVarProxyFunctionCode.Read));
        Assert.Equal(0x00, deviceError.DeviceErrorCode);

        byte[] wrongMsgId = FakeTransport.ParseHex("00 02 00 08 00 00 02 35 30 00 00 01");
        RobotException idError = Assert.Throws<RobotException>(
            () => KukaVarProxyResponseParser.ParseValueBytes(wrongMsgId, 1, KukaVarProxyFunctionCode.Read));
        Assert.Equal(RobotErrorKind.InvalidResponse, idError.Kind);

        RobotException shortError = Assert.Throws<RobotException>(
            () => KukaVarProxyResponseParser.ParseValueBytes(new byte[6], 1, KukaVarProxyFunctionCode.Read));
        Assert.Equal(RobotErrorKind.InvalidResponse, shortError.Kind);
    }

    [Fact]
    public async Task Client_ReadAndWriteEcho_IncrementMessageIdAndResetOnReconnect()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingHex("00 01 00 08 00 00 02 35 30 00 00 01");
        transport.EnqueueIncomingHex("00 02 00 08 01 00 02 37 35 00 00 01");

        using var client = new KukaVarProxyClient(transport);
        await client.OpenAsync();

        RobotResult<string> read = await client.ReadVariableAsync("$OV_PRO");
        Assert.True(read.IsSuccess);
        Assert.Equal("50", read.Value);

        RobotResult<string> write = await client.WriteVariableAsync("$OV_PRO", "75");
        Assert.True(write.IsSuccess);
        Assert.Equal("75", write.Value);

        transport.SimulateReconnect();
        transport.EnqueueIncomingHex("00 01 00 08 00 00 02 31 30 00 00 01");
        RobotResult<string> readAfterReconnect = await client.ReadVariableAsync("$OV_PRO");
        Assert.True(readAfterReconnect.IsSuccess);
        Assert.Equal("10", readAfterReconnect.Value);
    }
}
