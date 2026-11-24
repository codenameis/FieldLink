using System;
using System.Threading;
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

        // 출처: docs/plc-packet-manual/delta.md §1.7 "D100 1워드 읽기, 국번 1, RTU"
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
        Assert.Equal(0x03, sentPdu[0]);
        Assert.Equal(0x10, sentPdu[1]);
        Assert.Equal(0x64, sentPdu[2]);
    }

    [Fact]
    public async Task ReadAsync_Dvp_D4090_AcrossBoundary_SplitsIntoTwoChunks()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/delta.md §4 "D4090 + 10워드 -> [D4090, 6워드], [D4096, 4워드]"
        byte[] chunk1Pdu = [0x03, 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        byte[] chunk2Pdu = [0x03, 8, 21, 22, 23, 24, 25, 26, 27, 28];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D4090", PlcDataType.Int16, 10);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(20, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sent1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        ushort pdu1 = (ushort)((sent1[1] << 8) | sent1[2]);
        ushort count1 = (ushort)((sent1[3] << 8) | sent1[4]);
        Assert.Equal(0x1FFA, pdu1);
        Assert.Equal(6, count1);

        byte[] sent2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        ushort pdu2 = (ushort)((sent2[1] << 8) | sent2[2]);
        ushort count2 = (ushort)((sent2[3] << 8) | sent2[4]);
        Assert.Equal(0x9000, pdu2);
        Assert.Equal(4, count2);
    }

    [Fact]
    public async Task ReadAsync_As_X1_3_DiscreteInput_UsesFc02()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Tcp);

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
        Assert.Equal(0x02, sentPdu[0]);
        ushort pdu = (ushort)((sentPdu[1] << 8) | sentPdu[2]);
        Assert.Equal(24595, pdu);
    }

    [Fact]
    public async Task ReadAsync_As_X5_Word_InputRegister_UsesFc04()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Tcp);

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
        Assert.Equal(0x04, sentPdu[0]);
        ushort pdu = (ushort)((sentPdu[1] << 8) | sentPdu[2]);
        Assert.Equal(32773, pdu);
    }

    [Fact]
    public async Task WriteAsync_Dvp_D100_SingleWord_BuildsCorrectRtuFrame()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] pdu = [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, pdu);
        });

        byte[] dataToWrite = [0x12, 0x34];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, dataToWrite);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] pdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x10, pdu[0]);
        Assert.Equal(0x10, pdu[1]);
        Assert.Equal(0x64, pdu[2]);
    }

    [Fact]
    public async Task WriteAsync_Dvp_M0_TenBooleans_PacksBitsIntoTwoBytesWithFc0F()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/delta.md §1.2, §1.3 "M0 = 0x0800, 다중 비트 쓰기 FC 0x0F"
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x0F, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 10 bits: [1, 0, 1, 1, 0, 0, 0, 1] -> 0x8D, [1, 1] -> 0x03
        byte[] booleanBytes = [1, 0, 1, 1, 0, 0, 0, 1, 1, 1];
        var request = new PlcWriteRequest("M0", PlcDataType.Boolean, booleanBytes);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x0F, 0x08, 0x00, 0x00, 0x0A, 0x02, 0x8D, 0x03 }, sentPdu);
    }

    [Fact]
    public async Task ReadAsync_Dvp_D100_MultiElementInt32_ChunksByWordsPerElement()
    {
        var transport = new ScriptedTransport();
        var options = new DeltaOptions
        {
            Series = DeltaSeries.Dvp,
            Station = 1,
            MaxReadWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Rtu);

        // 청크 1: D100(0x1064)에서 4워드(8바이트 = Int32 2개)
        byte[] chunk1Pdu = [0x03, 0x08, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // 청크 2: D104(0x1068)에서 2워드(4바이트 = Int32 1개)
        byte[] chunk2Pdu = [0x03, 0x04, 0x09, 0x0A, 0x0B, 0x0C];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C }, result);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x03, 0x10, 0x64, 0x00, 0x04 }, sentPdu1);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x03, 0x10, 0x68, 0x00, 0x02 }, sentPdu2);
    }

    [Fact]
    public async Task WriteAsync_Dvp_D100_MultiElementInt32_SecondChunkFails_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new DeltaOptions
        {
            Series = DeltaSeries.Dvp,
            Station = 1,
            MaxWriteWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Rtu);

        // 첫 번째 청크(Int32 2개 = 4워드) 성공 응답
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 두 번째 청크(Int32 1개 = 2워드) 장치 예외 응답 (0x90, 0x02 Illegal Data Address)
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x90, 0x02]));

        byte[] payload = new byte[12];
        var request = new PlcWriteRequest("D100", PlcDataType.Int32, payload);

        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));
        Assert.Equal(2, exception.CompletedCount);

        Assert.Equal(2, transport.SentFrames.Count);
        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x10, sentPdu1[1]);
        Assert.Equal(0x64, sentPdu1[2]);
        Assert.Equal(0x04, sentPdu1[4]);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(0x10, sentPdu2[1]);
        Assert.Equal(0x68, sentPdu2[2]);
        Assert.Equal(0x02, sentPdu2[4]);
    }

    [Fact]
    public async Task ReadAndWriteBytes_Dvp_D100_OddByteLength_NormalizesToWordsAndTrims()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // 3바이트 읽기 -> 2워드(4바이트) 읽기 후 3바이트로 잘라 반환
        byte[] readResponsePdu = [0x03, 0x04, 0x41, 0x42, 0x43, 0x00];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, readResponsePdu));

        var readRequest = new PlcReadRequest("D100", PlcDataType.Byte, 3);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, readResult);

        // 홀수(3)바이트 쓰기 -> 인접 상위/하위 바이트 덮어쓰기 방지를 위해 ArgumentException 거부
        var oddWriteRequest = new PlcWriteRequest("D100", PlcDataType.Byte, new byte[] { 0x41, 0x42, 0x43 });
        await Assert.ThrowsAsync<ArgumentException>(() => protocol.WriteAsync(oddWriteRequest, default));
        Assert.Single(transport.SentFrames);

        // 짝수(4)바이트 쓰기 -> 2워드로 정규화하여 FC 0x10 전송
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            return ModbusRequestBuilder.BuildRtuAdu(1, [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]]);
        });

        var evenWriteRequest = new PlcWriteRequest("D100", PlcDataType.Byte, new byte[] { 0x41, 0x42, 0x43, 0x44 });
        await protocol.WriteAsync(evenWriteRequest, default);

        byte[] writePdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x10, 0x10, 0x64, 0x00, 0x02, 0x04, 0x41, 0x42, 0x43, 0x44 }, writePdu);
    }

    [Fact]
    public async Task ReadAndWriteAsync_DataTypeMismatch_ThrowsArgumentException()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("M0", PlcDataType.Int16, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Boolean, 1), default));
    }

    [Fact]
    public async Task WriteAsync_CallerCancellationAfterFirstChunk_PropagatesOperationCanceledException()
    {
        var transport = new ScriptedTransport();
        var options = new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1, MaxWriteWords = 1 };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Rtu);
        using var cts = new CancellationTokenSource();

        transport.EnqueueResponse(frame =>
        {
            cts.Cancel();
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            return ModbusRequestBuilder.BuildRtuAdu(1, [0x10, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]]);
        });
        transport.EnqueueResponse(_ => throw new OperationCanceledException(cts.Token));

        var request = new PlcWriteRequest("D100", PlcDataType.Int16, new byte[4]);
        await Assert.ThrowsAsync<OperationCanceledException>(() => protocol.WriteAsync(request, cts.Token));
    }

    [Fact]
    public async Task ReadAndWriteAsync_Dvp_D4095_Int32StraddlingDiscontinuousBoundary_ThrowsArgumentException()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        // D4095(0x1FFF) ~ D4096(0x9000) 불연속 경계에 2워드(Int32) 원소가 걸치면 즉시 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D4095", PlcDataType.Int32, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("D4095", PlcDataType.Int32, new byte[4]), default));
    }

    [Fact]
    public async Task ReadAsync_As_Y2Bit_UsesFc01At0xA020_MatchesSpecificationVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Tcp);

        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, [0x01, 0x01, 0x01]);
        });

        byte[] result = await protocol.ReadAsync(new PlcReadRequest("Y2", PlcDataType.Boolean, 1), default);
        Assert.Equal([1], result);

        // 출처: docs/plc-packet-manual/delta.md §4 "Y2 비트 접근 -> Coil 0xA020"
        byte[] sentPdu = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(new byte[] { 0x01, 0xA0, 0x20, 0x00, 0x01 }, sentPdu);
    }

    [Fact]
    public async Task ReadAndWriteAsync_As_D100_5_BitInWord_ReadsAndPerformsRmw()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.AS, Station = 1 }, DeltaTransportEncoding.Rtu);

        // 비트 5(0x0020)만 켜진 워드 응답
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x02, 0x00, 0x20]));
        byte[] bitRead = await protocol.ReadAsync(new PlcReadRequest("D100.5", PlcDataType.Boolean, 1), default);
        Assert.Equal([1], bitRead);

        // 기존 0x0001에서 비트 5 ON -> 0x0021 쓰기 검증
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x03, 0x02, 0x00, 0x01]));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x10, 0x00, 0x64, 0x00, 0x01]));
        await protocol.WriteAsync(new PlcWriteRequest("D100.5", PlcDataType.Boolean, [1]), default);

        byte[] writePdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[2], 1);
        Assert.Equal(new byte[] { 0x10, 0x00, 0x64, 0x00, 0x01, 0x02, 0x00, 0x21 }, writePdu);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Dvp_C199ToC200AndRangeOverflow_ThrowsBeforeSending()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(transport, new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 }, DeltaTransportEncoding.Rtu);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("C199", PlcDataType.Int16, 2), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("C199", PlcDataType.Int16, new byte[4]), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D9990", PlcDataType.Int16, 20), default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("D9990", PlcDataType.Int16, new byte[40]), default));

        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public async Task WriteAsync_UseSingleWriteFunctions_UsesFc05AndFc06()
    {
        var transport = new ScriptedTransport();
        var options = new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1, UseSingleWriteFunctions = true };
        var protocol = new DeltaProtocol(transport, options, DeltaTransportEncoding.Rtu);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x05, 0x08, 0x00, 0xFF, 0x00]));
        await protocol.WriteAsync(new PlcWriteRequest("M0", PlcDataType.Boolean, [1]), default);
        Assert.Equal(0x05, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1)[0]);

        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0x06, 0x10, 0x64, 0x12, 0x34]));
        await protocol.WriteAsync(new PlcWriteRequest("D100", PlcDataType.Int16, [0x12, 0x34]), default);
        Assert.Equal(0x06, ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1)[0]);
    }
}
