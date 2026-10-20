using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.GE;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.GE;

public class GESrtpProtocolTests
{
    [Fact]
    public async Task ConnectAsync_Sends56ZeroBytes_Validates0x01Response()
    {
        var transport = new ScriptedTransport();
        var protocol = new GESrtpProtocol(transport);

        byte[] initResponse = new byte[56];
        initResponse[0] = 0x01; // Init success
        transport.EnqueueResponse(initResponse);

        await protocol.ConnectAsync(default);

        Assert.Single(transport.SentFrames);
        Assert.Equal(56, transport.SentFrames[0].Length);
        Assert.All(transport.SentFrames[0], b => Assert.Equal(0, b));
    }

    [Fact]
    public async Task ReadAsync_M1_1Byte_MatchesTestVector_And_ExtractsShortPiggybackData()
    {
        var transport = new ScriptedTransport();
        var protocol = new GESrtpProtocol(transport);

        // Response: 56 bytes, status 0x00 at [42..43], msgType 0xD4 at [31], data 0x42 at [44]
        byte[] response = new byte[56];
        response[0] = 0x03; // Data response
        response[2] = 0x01; // Seq 1
        response[31] = 0xD4; // SHORT_ACK
        response[42] = 0x00; // Status OK
        response[43] = 0x00;
        response[44] = 0x42; // Data byte
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M1", PlcDataType.Byte, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(0x42, result[0]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(56, sent.Length);

        // Expected test vector from ge.md §4:
        byte[] expectedVector = [
            0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0xC0,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x04, 0x16, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task WriteAsync_M1_1Byte_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new GESrtpProtocol(transport);

        byte[] response = new byte[56];
        response[0] = 0x03;
        response[2] = 0x01;
        response[31] = 0xD4;
        response[42] = 0x00;
        response[43] = 0x00;
        transport.EnqueueResponse(response);

        byte[] data = [0x01];
        var request = new PlcWriteRequest("M1", PlcDataType.Byte, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(57, sent.Length);

        // Expected test vector from ge.md §4:
        byte[] expectedVector = [
            0x02, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x80,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x01, 0x07, 0x16, 0x00, 0x00, 0x01, 0x00, 0x01
        ];
        Assert.Equal(expectedVector, sent);
    }
}
