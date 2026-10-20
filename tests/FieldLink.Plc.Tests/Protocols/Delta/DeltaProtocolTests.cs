using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Delta;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Delta;

public class DeltaProtocolTests
{
    [Fact]
    public async Task ReadAsync_Dvp_D100_SingleWord_BuildsCorrectRtuFrameAndDecodes()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // Expected request: Station 1, FC 03, PDU 0x1064 (4196), Quantity 1 -> 01 03 10 64 00 01
        // Response: Station 1, FC 03, ByteCount 2, Data 0x1234 -> 01 03 02 12 34
        byte[] pduResponse = [0x03, 0x02, 0x12, 0x34];
        byte[] rtuResponse = ModbusRequestBuilder.BuildRtuAdu(1, pduResponse);
        transport.EnqueueResponse(rtuResponse);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x12, result[0]);
        Assert.Equal(0x34, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x03, sentPdu[0]); // FC 03
        Assert.Equal(0x10, sentPdu[1]); // PDU Hi
        Assert.Equal(0x64, sentPdu[2]); // PDU Lo
    }

    [Fact]
    public async Task ReadAsync_Dvp_D4090_AcrossBoundary_SplitsIntoTwoChunks()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // Chunk 1: D4090 ~ D4095 (6 words, PDU 4090 + 0x1000 = 8186 = 0x1FFA)
        byte[] chunk1Pdu = [0x03, 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // Chunk 2: D4096 ~ D4099 (4 words, PDU 0x9000, 4 words = 8 bytes)
        byte[] chunk2Pdu = [0x03, 8, 21, 22, 23, 24, 25, 26, 27, 28];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D4090", PlcDataType.Int16, 10);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(20, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sent1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        ushort pdu1 = (ushort)((sent1[1] << 8) | sent1[2]);
        ushort count1 = (ushort)((sent1[3] << 8) | sent1[4]);
        Assert.Equal(0x1FFA, pdu1); // 4090 + 0x1000
        Assert.Equal(6, count1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort count2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(0x9000, pdu2); // D4096 base
        Assert.Equal(4, count2);
    }

    [Fact]
    public async Task ReadAsync_As_X1_3_DiscreteInput_UsesFc02()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Tcp);

        // Expected PDU: 0x6000 + 16 + 3 = 0x6013 (24595)
        // Response: FC 02, ByteCount 1, Bit 1 (ON)
        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x02, 0x01, 0x01];
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("X1.3", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        byte[] sentPdu = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x02, sentPdu[0]); // FC 02
        ushort pdu = (ushort)((sentPdu[1] << 8) | sentPdu[2]);
        Assert.Equal(24595, pdu);
    }

    [Fact]
    public async Task ReadAsync_As_X5_Word_InputRegister_UsesFc04()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Tcp);

        // Expected PDU: 0x8000 + 5 = 0x8005 (32773), FC 04
        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x04, 0x02, 0x55, 0xAA];
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("X5", PlcDataType.UInt16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x55, result[0]);
        Assert.Equal(0xAA, result[1]);

        byte[] sentPdu = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x04, sentPdu[0]); // FC 04
        ushort pdu = (ushort)((sentPdu[1] << 8) | sentPdu[2]);
        Assert.Equal(32773, pdu);
    }

    [Fact]
    public async Task WriteAsync_Dvp_D100_SingleWord_BuildsCorrectRtuFrame()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // PDU 0x1064, write 1 register -> FC 16
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            // Echo FC 16, PDU Hi/Lo, Qty Hi/Lo
            byte[] pdu = [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, pdu);
        });

        byte[] dataToWrite = [0x12, 0x34];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, dataToWrite);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] pdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x10, pdu[0]); // FC 16 (0x10)
        Assert.Equal(0x10, pdu[1]); // PDU Hi
        Assert.Equal(0x64, pdu[2]); // PDU Lo
    }
}
