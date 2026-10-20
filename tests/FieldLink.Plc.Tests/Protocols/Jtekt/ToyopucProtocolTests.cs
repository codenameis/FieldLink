using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Jtekt;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Jtekt;

public class ToyopucProtocolTests
{
    [Fact]
    public async Task ReadAsync_D100_1Word_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // Response vector from §4: 80 00 03 00 1C 34 12
        byte[] response = [0x80, 0x00, 0x03, 0x00, 0x1C, 0x34, 0x12];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 05 00 1C 00 11 01 00
        byte[] expectedVector = [0x00, 0x00, 0x05, 0x00, 0x1C, 0x00, 0x11, 0x01, 0x00];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task WriteAsync_D100_2Words_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x01, 0x00, 0x1D];
        transport.EnqueueResponse(response);

        byte[] data = [0x34, 0x12, 0xAB, 0xCD];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 07 00 1D 00 11 34 12 AB CD
        byte[] expectedVector = [0x00, 0x00, 0x07, 0x00, 0x1D, 0x00, 0x11, 0x34, 0x12, 0xAB, 0xCD];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task ReadAsync_M10_Bit_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x02, 0x00, 0x20, 0x01];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M10", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 03 00 20 10 18
        byte[] expectedVector = [0x00, 0x00, 0x03, 0x00, 0x20, 0x10, 0x18];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task WriteAsync_M10_Bit_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x01, 0x00, 0x21];
        transport.EnqueueResponse(response);

        byte[] bitData = [1];
        var request = new PlcWriteRequest("M10", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 04 00 21 10 18 01
        byte[] expectedVector = [0x00, 0x00, 0x04, 0x00, 0x21, 0x10, 0x18, 0x01];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // Error vector from §4: 80 40 01 00 40
        byte[] response = [0x80, 0x40, 0x01, 0x00, 0x40];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        var ex = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal(0x40, ex.DeviceErrorCode);
        Assert.Contains("주소 범위 초과", ex.Message);
    }
}
