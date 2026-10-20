using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.FujiElectric;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.FujiElectric;

public class FujiElectricProtocolTests
{
    [Fact]
    public async Task ReadAsync_Spb_D100_1Word_BuildsCorrectAsciiAndDecodesData()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        // Expected request: :0109FFFF00000C00010100\r\n
        // Mock response: :0104FFFF0000003412\r\n
        byte[] response = Encoding.ASCII.GetBytes(":0104FFFF0000003412\r\n");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":0109FFFF00000C00010100\r\n", sent);
    }

    [Fact]
    public async Task WriteAsync_Spb_D100_2Words_BuildsExpectedRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        byte[] response = Encoding.ASCII.GetBytes(":0102FFFF000100\r\n");
        transport.EnqueueResponse(response);

        // 2 words: 0x1234, 0xCDAB in LE -> [0x34, 0x12, 0xAB, 0xCD]
        byte[] data = [0x34, 0x12, 0xAB, 0xCD];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":010DFFFF01000C000102003412ABCD\r\n", sent);
    }

    [Fact]
    public async Task WriteAsync_Spb_SingleBit_BuildsBitWriteCommand()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        byte[] response = Encoding.ASCII.GetBytes(":0102FFFF000102\r\n");
        transport.EnqueueResponse(response);

        byte[] bitData = [1];
        var request = new PlcWriteRequest("M1000", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":0109FFFF01020225010001\r\n", sent);
    }

    [Fact]
    public async Task ReadAsync_Sph_M1_100_1Word_MatchesSpecificationTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSphProtocol(transport, new FujiElectricSphOptions { ConnectionId = 1 });

        // SPH response: 20-byte header (status 0x00 at [4]) + 6-byte descriptor + 2-byte data
        byte[] response = new byte[28];
        response[0] = 0xFB;
        response[1] = 0x80;
        response[2] = 0x80;
        response[4] = 0x00; // Success
        response[5] = 0x7B;
        response[6] = 0x01; // Connection ID echo
        response[14] = 0x00; // Read echo
        response[17] = 0x01;
        response[18] = 0x08; // 8 bytes payload (6 descriptor + 2 data)
        // Descriptor
        response[20] = 0x02; // M1
        response[21] = 0x64;
        response[24] = 0x01;
        // Data: 0x1234
        response[26] = 0x34;
        response[27] = 0x12;

        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M1.100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Match exact test vector from fuji.md §C.3
        byte[] expectedVector = [
            0xFB, 0x80, 0x80, 0x00, 0xFF, 0x7B, 0x01, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x06, 0x00,
            0x02, 0x64, 0x00, 0x00, 0x01, 0x00
        ];
        Assert.Equal(expectedVector, sent);
    }
}
