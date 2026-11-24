using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
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

    [Fact]
    public async Task Traits_DefaultByteOrder_IsBigEndianWordSwap_AndPlcClientDecodesCdab()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.H3u, Station = 1 }, InovanceTransportEncoding.Tcp);

        // 출처: docs/plc-packet-manual/inovance.md §2.2 "기본 32비트 워드 순서는 하위 워드 먼저(CDAB)"
        Assert.Equal(ByteOrder.BigEndianWordSwap, protocol.Traits.DefaultByteOrder);
        Assert.Equal(0x12345678, PlcValueConverter.ToInt32([0x56, 0x78, 0x12, 0x34], byteOrder: protocol.Traits.DefaultByteOrder));

        using var client = new PlcClient(transport, protocol);
        PlcResult openResult = await client.OpenAsync();
        Assert.True(openResult.IsSuccess);

        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, [0x03, 0x04, 0x56, 0x78, 0x12, 0x34]);
        });

        PlcResult<int> readResult = await client.ReadInt32Async("D100");
        Assert.True(readResult.IsSuccess);
        Assert.Equal(0x12345678, readResult.Value);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Am_SmxAndSdw_UsesExtendedFunctionCodePlus0x30_Rtu()
    {
        var transport = new ScriptedTransport();
        var options = new InovanceOptions { Series = InovanceSeries.AM, Station = 1 };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        // SM10 읽기 -> FC 0x01 + 0x30 = 0x31
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x31, 0x01, 0x01]));
        byte[] smRead = await protocol.ReadAsync(new PlcReadRequest("SM10", PlcDataType.Boolean, 1), default);
        Assert.Equal([1], smRead);
        Assert.Equal(0x31, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1)[0]);

        // SDW10 읽기 -> FC 0x03 + 0x30 = 0x33
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x33, 0x02, 0x12, 0x34]));
        byte[] sdwRead = await protocol.ReadAsync(new PlcReadRequest("SDW10", PlcDataType.Int16, 1), default);
        Assert.Equal([0x12, 0x34], sdwRead);
        Assert.Equal(0x33, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1)[0]);

        // SM10 다중 비트 쓰기 -> FC 0x0F + 0x30 = 0x3F
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x3F, 0x00, 0x0A, 0x00, 0x02]));
        await protocol.WriteAsync(new PlcWriteRequest("SM10", PlcDataType.Boolean, [1, 0]), default);
        Assert.Equal(0x3F, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[2], 1)[0]);

        // UseSingleWriteFunctions = true일 때 SMX0 -> 0x35, SDW10 -> 0x36
        var singleProtocol = new InovanceProtocol(
            transport,
            new InovanceOptions { Series = InovanceSeries.AM, Station = 1, UseSingleWriteFunctions = true },
            InovanceTransportEncoding.Rtu);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x35, 0x00, 0x00, 0xFF, 0x00]));
        await singleProtocol.WriteAsync(new PlcWriteRequest("SMX0", PlcDataType.Boolean, [1]), default);
        Assert.Equal(0x35, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[3], 1)[0]);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x36, 0x00, 0x0A, 0x12, 0x34]));
        await singleProtocol.WriteAsync(new PlcWriteRequest("SDW10", PlcDataType.Int16, [0x12, 0x34]), default);
        Assert.Equal(0x36, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[4], 1)[0]);

        // RTU 디코더가 확장 예외 응답(0x33 | 0x80 = 0xB3) 프레임 경계도 올바르게 판정하는지 검증
        var rtuDecoder = new InovanceRtuFrameDecoder();
        byte[] extendedExceptionFrame = ModbusRequestBuilder.BuildRtuAdu(1, [0xB3, 0x02]);
        bool isComplete = rtuDecoder.TryGetFrameLength(new ArraySegment<byte>(extendedExceptionFrame), out int frameLength);
        Assert.True(isComplete);
        Assert.Equal(5, frameLength);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Am_MbByteAddress_ReadsOddOffsetAndPerformsRmwWrite()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.AM, Station = 1 }, InovanceTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/inovance.md §2.3
        // MB1부터 3바이트(MB1, MB2, MB3) 읽기 -> MW0, MW1 총 2워드 읽기
        // MW0 = [0xAA(MB1), 0x11(MB0)], MW1 = [0x22(MB3), 0x33(MB2)]
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0xAA, 0x11, 0x22, 0x33]));
        byte[] mbSlice = await protocol.ReadAsync(new PlcReadRequest("MB1", PlcDataType.Byte, 3), default);
        Assert.Equal([0xAA, 0x33, 0x22], mbSlice);

        byte[] readWordPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x02 }, readWordPdu);

        // MB1 1바이트 쓰기(0xBB) -> MW0 읽기 후 상위 바이트만 0xBB로 수정하고 하위 바이트(MB0=0x11) 보존
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x02, 0xAA, 0x11]));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x10, 0x00, 0x00, 0x00, 0x01]));
        await protocol.WriteAsync(new PlcWriteRequest("MB1", PlcDataType.Byte, [0xBB]), default);

        byte[] rmwWritePdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[2], 1);
        Assert.Equal(new byte[] { 0x10, 0x00, 0x00, 0x00, 0x01, 0x02, 0xBB, 0x11 }, rmwWritePdu);
    }

    [Fact]
    public async Task ReadAsync_AmMdAndH3uC200_AllowsNon4ByteMultipleByteAndString_WhileWriteRejects()
    {
        var transport = new ScriptedTransport();
        var amProtocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.AM, Station = 1 }, InovanceTransportEncoding.Rtu);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x41, 0x42, 0x43, 0x00]));
        byte[] bytes = await amProtocol.ReadAsync(new PlcReadRequest("MD100", PlcDataType.Byte, 3), default);
        Assert.Equal([0x41, 0x42, 0x43], bytes);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x04, 0x41, 0x42, 0x43, 0x00]));
        byte[] str = await amProtocol.ReadAsync(new PlcReadRequest("MD100", PlcDataType.String, 3), default);
        Assert.Equal([0x41, 0x42, 0x43], str);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            amProtocol.WriteAsync(new PlcWriteRequest("MD100", PlcDataType.Byte, [0x41, 0x42, 0x43]), default));
    }

    [Fact]
    public async Task Options_And_Protocol_EnforceSeriesValidationUseSingleWriteAndUpfrontOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InovanceOptions { Series = (InovanceSeries)999 });

        var transport = new ScriptedTransport();
        var options = new InovanceOptions { Series = InovanceSeries.AM, Station = 1, UseSingleWriteFunctions = true };
        var protocol = new InovanceProtocol(transport, options, InovanceTransportEncoding.Rtu);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x06, 0x00, 0x0A, 0x12, 0x34]));
        await protocol.WriteAsync(new PlcWriteRequest("MW10", PlcDataType.Int16, [0x12, 0x34]), default);
        Assert.Equal(0x06, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1)[0]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            protocol.ReadAsync(new PlcReadRequest("MW65530", PlcDataType.Int16, 10), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("MW65530", PlcDataType.Int16, new byte[20]), default));
        Assert.Single(transport.SentFrames);
    }
}
