using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Fatek;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Fatek;

public class FatekProtocolTests
{
    private static byte[] MakeResponse(byte station, string command, char status, string data = "")
    {
        string headerAndData = $"{station:X2}{command}{status}{data}";
        int sum = 0x02;
        foreach (char c in headerAndData)
        {
            sum += (byte)c;
        }

        string lrc = (sum & 0xFF).ToString("X2");
        string frame = $"\x02{headerAndData}{lrc}\x03";
        return Encoding.ASCII.GetBytes(frame);
    }

    [Fact]
    public async Task ReadAsync_DiscreteBits_Command44_BuildsRequestAndExtractsBits()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        // Station 1, Cmd 44, Status 0, 4 bits: "1010"
        byte[] response = MakeResponse(1, "44", '0', "1010");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 4);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        Assert.Equal(1, result[0]);
        Assert.Equal(0, result[1]);
        Assert.Equal(1, result[2]);
        Assert.Equal(0, result[3]);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014404X0000", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task ReadAsync_WordRegisters_Command46_BuildsRequestAndDecodesLittleEndian()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        // Station 1, Cmd 46, Status 0, 2 words: 0x1234, 0xABCD -> "1234ABCD"
        byte[] response = MakeResponse(1, "46", '0', "1234ABCD");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("R100", PlcDataType.Int16, 2);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        // Little endian: 0x1234 -> [0x34, 0x12]
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);
        // Little endian: 0xABCD -> [0xCD, 0xAB]
        Assert.Equal(0xCD, result[2]);
        Assert.Equal(0xAB, result[3]);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014602R00100", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task WriteAsync_Bits_Command45_BuildsCorrectRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "45", '0');
        transport.EnqueueResponse(response);

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("Y0", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014503Y0000101", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task WriteAsync_Words_Command47_BuildsCorrectRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "47", '0');
        transport.EnqueueResponse(response);

        // Word 0x1234 in little-endian byte array [0x34, 0x12]
        byte[] wordData = [0x34, 0x12];
        var request = new PlcWriteRequest("D0", PlcDataType.Int16, wordData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014701D000001234", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        // Error code '2' - Illegal value
        byte[] response = MakeResponse(1, "44", '2');
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        var ex = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal((int)'2', ex.DeviceErrorCode);
        Assert.Contains("Illegal value", ex.Message);
    }
}
