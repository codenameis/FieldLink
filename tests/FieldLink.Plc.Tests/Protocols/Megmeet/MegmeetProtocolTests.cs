using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Megmeet;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Megmeet;

public class MegmeetProtocolTests
{
    [Fact]
    public async Task ReadAsync_M2000_100Bits_SplitsAcrossBoundary2048()
    {
        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // Chunk 1: M2000 ~ M2047 (48 bits = 6 bytes, PDU 2000 + 2000 = 4000 = 0x0FA0)
        byte[] chunk1Pdu = [0x01, 6, 1, 2, 3, 4, 5, 6];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // Chunk 2: M2048 ~ M2099 (52 bits = 7 bytes, PDU 12000 = 0x2EE0)
        byte[] chunk2Pdu = [0x01, 7, 7, 8, 9, 10, 11, 12, 13];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("M2000", PlcDataType.Boolean, 100);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(100, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sent1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        ushort pdu1 = (ushort)((sent1[1] << 8) | sent1[2]);
        ushort qty1 = (ushort)((sent1[3] << 8) | sent1[4]);
        Assert.Equal(4000, pdu1); // 2000 + 2000
        Assert.Equal(48, qty1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort qty2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(12000, pdu2); // M2048 base
        Assert.Equal(52, qty2);
    }

    [Fact]
    public async Task ReadAsync_SD200_100Words_SplitsAcrossBoundary256()
    {
        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // Chunk 1: SD200 ~ SD255 (56 words = 112 bytes, PDU 200 + 8000 = 8200 = 0x2008)
        byte[] chunk1Pdu = new byte[2 + 112];
        chunk1Pdu[0] = 0x03;
        chunk1Pdu[1] = 112;
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // Chunk 2: SD256 ~ SD299 (44 words = 88 bytes, PDU 12000 = 0x2EE0)
        byte[] chunk2Pdu = new byte[2 + 88];
        chunk2Pdu[0] = 0x03;
        chunk2Pdu[1] = 88;
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("SD200", PlcDataType.Int16, 100);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(200, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sent1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        ushort pdu1 = (ushort)((sent1[1] << 8) | sent1[2]);
        ushort qty1 = (ushort)((sent1[3] << 8) | sent1[4]);
        Assert.Equal(8200, pdu1); // 200 + 8000
        Assert.Equal(56, qty1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort qty2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(12000, pdu2); // SD256 base
        Assert.Equal(44, qty2);
    }
}
