using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Inovance;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Inovance;

public class InovanceProtocolTests
{
    [Fact]
    public async Task ReadAsync_H3u_D100_Tcp_BuildsMbapAndDecodesRegisters()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.H3u, Station = 1 }, InovanceTransportEncoding.Tcp);

        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x03, 0x02, 0x12, 0x34];
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x12, result[0]);
        Assert.Equal(0x34, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] pduSent = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x03, pduSent[0]);
        Assert.Equal(0x00, pduSent[1]);
        Assert.Equal(0x64, pduSent[2]);
    }

    [Fact]
    public async Task ReadAsync_Am_IX10_UsesDiscreteInputFC02()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.AM, Station = 1 }, InovanceTransportEncoding.Tcp);

        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x02, 0x01, 0x01];
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("IX10", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        byte[] pduSent = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x02, pduSent[0]);
        Assert.Equal(0x00, pduSent[1]);
        Assert.Equal(10, pduSent[2]);
    }

    [Fact]
    public async Task WriteAsync_H3u_M0_TenBooleans_PacksBitsIntoTwoBytesWithFc0F()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.H3u, Station = 1 }, InovanceTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/inovance.md §1.2, §1.4 "H3U M0 = 0x0000, 다중 비트 쓰기 FC 0x0F"
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x0F, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 10 bits: [1, 0, 1, 0, 1, 0, 1, 0] -> 0x55, [0, 1] -> 0x02
        byte[] booleanBytes = [1, 0, 1, 0, 1, 0, 1, 0, 0, 1];
        var request = new PlcWriteRequest("M0", PlcDataType.Boolean, booleanBytes);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x0F, 0x00, 0x00, 0x00, 0x0A, 0x02, 0x55, 0x02 }, sentPdu);
    }

    [Fact]
    public async Task ReadAsync_H3u_D100_MultiElementInt32_ChunksByWordsPerElement()
    {
        var transport = new ScriptedTransport();
        var options = new InovanceOptions
        {
            Series = InovanceSeries.H3u,
            Station = 1,
            MaxReadWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        // 청크 1: D100(0x0064)에서 4워드(8바이트 = Int32 2개)
        byte[] chunk1Pdu = [0x03, 0x08, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // 청크 2: D104(0x0068)에서 2워드(4바이트 = Int32 1개)
        byte[] chunk2Pdu = [0x03, 0x04, 0x99, 0xAA, 0xBB, 0xCC];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC }, result);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x64, 0x00, 0x04 }, sentPdu1);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x68, 0x00, 0x02 }, sentPdu2);
    }

    [Fact]
    public async Task WriteAsync_H3u_D100_MultiElementInt32_SecondChunkFails_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new InovanceOptions
        {
            Series = InovanceSeries.H3u,
            Station = 1,
            MaxWriteWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        // 첫 번째 청크(Int32 2개 = 4워드) 성공 응답
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 두 번째 청크 장치 예외 응답 (0x90, 0x02)
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
    public async Task ReadAndWriteAsync_H3u_32BitCounterC200_StepsByTwoWordsAndRejectsBoundaryOr16Bit()
    {
        var transport = new ScriptedTransport();
        var options = new InovanceOptions
        {
            Series = InovanceSeries.H3u,
            Station = 1,
            MaxReadWords = 2, // 32비트 카운터 1개(2워드)씩 청크 분할 검증
            MaxWriteWords = 2,
        };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        // C200에 16비트 요청 거부 및 C199 -> C200 경계 침범 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("C200", PlcDataType.Int16, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("C199", PlcDataType.Int16, 2), default));

        // C200(0xF700) 및 C201(0xF702) 연속 읽기
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x00, 0x01, 0x00, 0x02]));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x00, 0x03, 0x00, 0x04]));

        byte[] readBytes = await protocol.ReadAsync(new PlcReadRequest("C200", PlcDataType.Int32, 2), default);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04 }, readBytes);

        byte[] readPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0xF7, 0x00, 0x00, 0x02 }, readPdu1);

        byte[] readPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x03, 0xF7, 0x02, 0x00, 0x02 }, readPdu2);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Am_MD100_32BitDWord_StepsByTwoWordsAndRejects16Bit()
    {
        var md100 = InovanceAddress.Parse("MD100", InovanceSeries.AM, isBitAccess: false);
        Assert.True(md100.Is32BitWord);
        Assert.Equal((ushort)200, md100.PduAddress);
        Assert.Equal((ushort)202, md100.Offset(1).PduAddress);

        var transport = new ScriptedTransport();
        var options = new InovanceOptions
        {
            Series = InovanceSeries.AM,
            Station = 1,
            MaxReadWords = 2, // 32비트 MD 레지스터 1개(2워드)씩 청크 분할 검증
            MaxWriteWords = 2,
        };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        // MD100에 16비트 요청 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("MD100", PlcDataType.Int16, 1), default));

        // MD100(PDU 200 = 0x00C8) 및 MD101(PDU 202 = 0x00CA) 연속 읽기
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x11, 0x22, 0x33, 0x44]));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x55, 0x66, 0x77, 0x88]));

        byte[] readBytes = await protocol.ReadAsync(new PlcReadRequest("MD100", PlcDataType.Int32, 2), default);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }, readBytes);

        byte[] readPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0xC8, 0x00, 0x02 }, readPdu1);

        byte[] readPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0xCA, 0x00, 0x02 }, readPdu2);
    }
}
