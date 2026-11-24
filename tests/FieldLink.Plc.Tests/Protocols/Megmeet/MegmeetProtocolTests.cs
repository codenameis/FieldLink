using System;
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

        // 출처: docs/plc-packet-manual/megmeet.md §4 "M2000 + 100비트 -> [M2000, 48비트], [M2048, 52비트]"
        byte[] chunk1Pdu = [0x01, 6, 1, 2, 3, 4, 5, 6];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        byte[] chunk2Pdu = [0x01, 7, 7, 8, 9, 10, 11, 12, 13];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("M2000", PlcDataType.Boolean, 100);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(100, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sent1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        ushort pdu1 = (ushort)((sent1[1] << 8) | sent1[2]);
        ushort qty1 = (ushort)((sent1[3] << 8) | sent1[4]);
        Assert.Equal(4000, pdu1);
        Assert.Equal(48, qty1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort qty2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(12000, pdu2);
        Assert.Equal(52, qty2);
    }

    [Fact]
    public async Task ReadAsync_SD200_100Words_SplitsAcrossBoundary256()
    {
        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/megmeet.md §4 "SD200 + 100워드 -> [SD200, 56워드], [SD256, 44워드]"
        byte[] chunk1Pdu = new byte[2 + 112];
        chunk1Pdu[0] = 0x03;
        chunk1Pdu[1] = 112;
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

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
        Assert.Equal(8200, pdu1);
        Assert.Equal(56, qty1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort qty2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(12000, pdu2);
        Assert.Equal(44, qty2);
    }

    [Fact]
    public async Task WriteAsync_M0_TenBooleans_PacksBitsIntoTwoBytesWithFc0F()
    {
        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/megmeet.md §1.2 "M0 = 2000 (0x07D0), 다중 비트 쓰기 FC 0x0F"
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x0F, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 10 bits: [1, 1, 0, 0, 1, 1, 0, 0] -> 0x33, [1, 0] -> 0x01
        byte[] booleanBytes = [1, 1, 0, 0, 1, 1, 0, 0, 1, 0];
        var request = new PlcWriteRequest("M0", PlcDataType.Boolean, booleanBytes);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x0F, 0x07, 0xD0, 0x00, 0x0A, 0x02, 0x33, 0x01 }, sentPdu);
    }

    [Fact]
    public async Task ReadAsync_D100_MultiElementInt32_ChunksByWordsPerElement()
    {
        var transport = new ScriptedTransport();
        var options = new MegmeetOptions
        {
            Station = 1,
            MaxReadWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new MegmeetProtocol(transport, options, MegmeetTransportEncoding.Rtu);

        // 청크 1: D100(0x0064)에서 4워드(8바이트 = Int32 2개)
        byte[] chunk1Pdu = [0x03, 0x08, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // 청크 2: D104(0x0068)에서 2워드(4바이트 = Int32 1개)
        byte[] chunk2Pdu = [0x03, 0x04, 0x09, 0x0A, 0x0B, 0x0C];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C }, result);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x64, 0x00, 0x04 }, sentPdu1);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x68, 0x00, 0x02 }, sentPdu2);
    }

    [Fact]
    public async Task WriteAsync_D100_MultiElementInt32_SecondChunkFails_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new MegmeetOptions
        {
            Station = 1,
            MaxWriteWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new MegmeetProtocol(transport, options, MegmeetTransportEncoding.Rtu);

        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x90, 0x02]));

        byte[] payload = new byte[12];
        var request = new PlcWriteRequest("D100", PlcDataType.Int32, payload);

        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));
        Assert.Equal(2, exception.CompletedCount);

        Assert.Equal(2, transport.SentFrames.Count);
        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x00, sentPdu1[1]);
        Assert.Equal(0x64, sentPdu1[2]);
        Assert.Equal(0x04, sentPdu1[4]);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(0x00, sentPdu2[1]);
        Assert.Equal(0x68, sentPdu2[2]);
        Assert.Equal(0x02, sentPdu2[4]);
    }

    [Fact]
    public async Task ReadAndWriteAsync_SD255_Int32StraddlingBoundaryAndC199ToC200Crossing_ThrowsArgumentException()
    {
        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // SD255(8255) ~ SD256(12000) 불연속 경계에 2워드(Int32) 원소가 걸치면 즉시 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("SD255", PlcDataType.Int32, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("SD255", PlcDataType.Int32, new byte[4]), default));

        // 16비트 카운터 C199(9699)와 32비트 카운터 C200(9700) 경계를 가로지르는 워드 요청 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("C199", PlcDataType.Int16, 2), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("C199", PlcDataType.Int16, new byte[4]), default));
    }
}
