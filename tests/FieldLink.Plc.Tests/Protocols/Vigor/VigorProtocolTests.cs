using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Vigor;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Vigor;

public class VigorProtocolTests
{
    [Fact]
    public void BuildReadRequest_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = VigorAddress.Parse("D100", isBitAccess: false);
        byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(station: 1, parsedAddress, count: 1);

        byte[] expected =
        [
            0x10, 0x02, 0x01, 0x07, 0x00, 0x20, 0xA0, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x03, 0x43, 0x41
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void BuildReadRequest_M10_8Bits_WithDleStuffing_MatchesTestVector()
    {
        var parsedAddress = VigorAddress.Parse("M10", isBitAccess: true);
        byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(station: 1, parsedAddress, count: 8);

        byte[] expected =
        [
            0x10, 0x02, 0x01, 0x07, 0x00, 0x21, 0x92, 0x10, 0x10, 0x00, 0x00, 0x08, 0x00, 0x10, 0x03, 0x44, 0x33
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void ParseResponse_ValidReadWord_ReturnsData()
    {
        // Body unescaped: [01, 03, 00, 00, 34, 12] -> sum = 1 + 3 + 0 + 0 + 0x34 + 0x12 = 74 = 0x4A ('4', 'A')
        byte[] response =
        [
            0x10, 0x02, 0x01, 0x03, 0x00, 0x00, 0x34, 0x12, 0x10, 0x03, 0x34, 0x41
        ];

        byte[] data = VigorResponseParser.ParseResponse(response, expectedStation: 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void ParseResponse_ErrorCode_ThrowsInvalidOperationException()
    {
        // Error 0x06: Body [01, 01, 00, 06] -> sum = 1 + 1 + 0 + 6 = 8 -> "08" (0x30, 0x38)
        byte[] response =
        [
            0x10, 0x02, 0x01, 0x01, 0x00, 0x06, 0x10, 0x03, 0x30, 0x38
        ];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            VigorResponseParser.ParseResponse(response, expectedStation: 1));

        Assert.Contains("0x06", ex.Message);
    }

    [Fact]
    public async Task Protocol_ReadAsync_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new VigorProtocol(transport, new VigorOptions { StationNumber = 1 });

        transport.EnqueueResponse(new byte[]
        {
            0x10, 0x02, 0x01, 0x03, 0x00, 0x00, 0x34, 0x12, 0x10, 0x03, 0x34, 0x41
        });

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
        Assert.Single(transport.SentFrames);
    }

    [Fact]
    public async Task Protocol_WriteAsync_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new VigorProtocol(transport, new VigorOptions { StationNumber = 1 });

        // Write ACK response: body [01, 01, 00, 00] -> sum = 2 -> "02"
        transport.EnqueueResponse(new byte[]
        {
            0x10, 0x02, 0x01, 0x01, 0x00, 0x00, 0x10, 0x03, 0x30, 0x32
        });

        var requestFrame = new PlcWriteRequest("D100", PlcDataType.Int16, new byte[] { 0x34, 0x12 });
        await protocol.WriteAsync(requestFrame, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x10, sent[0]);
        Assert.Equal(0x02, sent[1]);
        Assert.Equal(0x01, sent[2]);
    }
}
