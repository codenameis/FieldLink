using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Panasonic;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Panasonic;

public class MewtocolProtocolTests
{
    private static byte[] MakeResponse(byte station, char status, string body)
    {
        string prefix = $"%{station:X2}{status}{body}";
        byte bcc = 0;
        foreach (char c in prefix)
        {
            bcc ^= (byte)c;
        }

        string full = $"{prefix}{bcc:X2}\r";
        return Encoding.ASCII.GetBytes(full);
    }

    [Fact]
    public async Task ReadAsync_X0_SingleCoil_MatchesSpecificationVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // Mock response: %01$RC115\r (Bit 1 ON)
        byte[] response = MakeResponse(1, '$', "RC1");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        // Exact vector from §4: %01#RCSX00001D\r
        Assert.Equal("%01#RCSX00001D\r", sent);
    }

    [Fact]
    public async Task ReadAsync_DT0_2Words_MatchesSpecificationVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // 2 words: 0x1234, 0xABCD in LE -> "3412ABCD"
        byte[] response = MakeResponse(1, '$', "RD3412ABCD");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("DT0", PlcDataType.Int16, 2);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);
        Assert.Equal(0xAB, result[2]);
        Assert.Equal(0xCD, result[3]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        // BCC of %01#RDD0000000001 is 54
        Assert.Equal("%01#RDD000000000154\r", sent);
    }

    [Fact]
    public async Task WriteAsync_DT100_2Words_BuildsExpectedRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE });

        byte[] response = MakeResponse(0xEE, '$', "WD");
        transport.EnqueueResponse(response);

        byte[] data = [0x34, 0x12, 0xCD, 0xAB];
        var request = new PlcWriteRequest("DT100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("%EE#WDD00100001013412CDAB", sent);
        Assert.EndsWith("\r", sent);
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // Error code 40 (BCC error)
        byte[] response = MakeResponse(1, '!', "40");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        var ex = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal(40, ex.DeviceErrorCode);
        Assert.Contains("BCC", ex.Message);
    }
}
