using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Yokogawa;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Yokogawa;

public class YokogawaProtocolTests
{
    [Fact]
    public void BuildReadRequest_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = YokogawaAddress.Parse("D100", isBitAccess: false);
        byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(cpuNumber: 1, parsedAddress, count: 1);

        // Vector from §4: 11 01 00 08 00 04 00 00 00 64 00 01
        byte[] expected =
        [
            0x11, 0x01, 0x00, 0x08, 0x00, 0x04, 0x00, 0x00, 0x00, 0x64, 0x00, 0x01
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void ParseReadResponse_ValidWordRead_ReturnsData()
    {
        // Subheader: 0x11 | 0x80 = 0x91, EndCode: 0x00, Size: 0x00 0x02, Data: 0x34 0x12
        byte[] response = [0x91, 0x00, 0x00, 0x02, 0x34, 0x12];
        byte[] data = YokogawaResponseParser.ParseReadResponse(response, expectedCommand: 0x11);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void ParseReadResponse_DeviceError_ThrowsPlcDeviceException()
    {
        // Subheader 0x91, EndCode: 0x03 (ER03 디바이스 오류)
        byte[] response = [0x91, 0x03, 0x00, 0x00];

        var ex = Assert.Throws<PlcDeviceException>(() =>
            YokogawaResponseParser.ParseReadResponse(response, expectedCommand: 0x11));

        Assert.Equal(0x03, ex.DeviceErrorCode);
        Assert.Contains("ER03", ex.Message);
    }

    [Fact]
    public async Task Protocol_ReadWord_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new YokogawaProtocol(transport, new YokogawaOptions { CpuNumber = 1 });

        byte[] response = [0x91, 0x00, 0x00, 0x02, 0x34, 0x12];
        transport.EnqueueResponse(response);

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x11, sent[0]);
    }

    [Fact]
    public async Task Protocol_WriteWord_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new YokogawaProtocol(transport, new YokogawaOptions { CpuNumber = 1 });

        byte[] response = [0x92, 0x00, 0x00, 0x00];
        transport.EnqueueResponse(response);

        var requestFrame = new PlcWriteRequest("D100", PlcDataType.Int16, new byte[] { 0x34, 0x12 });
        await protocol.WriteAsync(requestFrame, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x12, sent[0]);
    }
}
