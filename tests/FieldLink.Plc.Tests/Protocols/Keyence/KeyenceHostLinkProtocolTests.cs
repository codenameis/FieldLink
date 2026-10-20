using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Keyence;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Keyence;

public class KeyenceHostLinkProtocolTests
{
    [Fact]
    public async Task ConnectAsync_SendsCR_ValidatesCCResponse()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("CC\r\n"));
        await protocol.ConnectAsync(default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("CR\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task ReadAsync_DM100_1Word_BuildsRDSAndParsesDecimal()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        // 4660 is 0x1234
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("4660\r\n"));

        var request = new PlcReadRequest("DM100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        Assert.Equal("RDS DM100 1\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task WriteAsync_DM100_2Words_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));

        // 2 words: 0x1234 (4660) and 0xABCD (43981) in LE: [0x34, 0x12, 0xCD, 0xAB]
        byte[] data = [0x34, 0x12, 0xCD, 0xAB];
        var request = new PlcWriteRequest("DM100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("WRS DM100 2 4660 43981\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task WriteAsync_MR100_3Bits_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("MR100", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("WRS MR100 3 1 0 1\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("E0\r\n"));

        var request = new PlcReadRequest("DM100", PlcDataType.Int16, 1);
        var ex = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal(0, ex.DeviceErrorCode);
        Assert.Contains("디바이스 번호 이상", ex.Message);
    }
}
