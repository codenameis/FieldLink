using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Protocols.Xinje;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Xinje;

public class XinjeProtocolTests
{
    [Fact]
    public void BuildExtendedReadPdu_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("D100", XinjeSeries.XdXl, isBitAccess: false);
        byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(parsedAddress, count: 1);

        // 출처: docs/plc-packet-manual/xinje.md §A.3 "워드 읽기 1개 D100 -> 01 20 80 00 00 64 00 01"
        byte[] expected = [0x20, 0x80, 0x00, 0x00, 0x64, 0x00, 0x01];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedReadPdu_M10_8Bits_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("M10", XinjeSeries.XdXl, isBitAccess: true);
        byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(parsedAddress, count: 8);

        // 출처: docs/plc-packet-manual/xinje.md §A.3 "비트 읽기 8개 M10 -> 01 1E 03 00 00 0A 00 08"
        byte[] expected = [0x1E, 0x03, 0x00, 0x00, 0x0A, 0x00, 0x08];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedWriteWordPdu_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("D100", XinjeSeries.XdXl, isBitAccess: false);
        byte[] data = [0x34, 0x12];
        byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteWordPdu(parsedAddress, count: 1, data);

        // 출처: docs/plc-packet-manual/xinje.md §A.3 "워드 쓰기 1개 D100=34 12 -> 01 21 80 00 00 64 00 01 02 34 12"
        byte[] expected = [0x21, 0x80, 0x00, 0x00, 0x64, 0x00, 0x01, 0x02, 0x34, 0x12];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedWriteBitPdu_M10_3Bits_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("M10", XinjeSeries.XdXl, isBitAccess: true);
        bool[] bits = [true, false, true];
        byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteBitPdu(parsedAddress, count: 3, bits);

        // 출처: docs/plc-packet-manual/xinje.md §A.3 "비트 쓰기 3개 M10=1,0,1 -> 01 1F 03 00 00 0A 00 03 01 05"
        byte[] expected = [0x1F, 0x03, 0x00, 0x00, 0x0A, 0x00, 0x03, 0x01, 0x05];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public async Task Protocol_ReadExtendedWord_Rtu_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new XinjeProtocol(transport, new XinjeOptions { Station = 1 }, XinjeTransportEncoding.Rtu);

        byte[] response = ModbusRequestBuilder.BuildRtuAdu(1, [0x20, 0x02, 0x12, 0x34]);
        transport.EnqueueResponse(response);

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x01, sent[0]);
        Assert.Equal(0x20, sent[1]);
    }

    [Fact]
    public async Task Protocol_ReadStandardModbus_Xc_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions { Series = XinjeSeries.XC, Station = 1 };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        byte[] response = [0x01, 0x03, 0x02, 0x12, 0x34, 0xB5, 0x33];
        transport.EnqueueResponse(response);

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x01, sent[0]);
        Assert.Equal(0x03, sent[1]);
    }

    [Fact]
    public async Task WriteAsync_StandardModbus_M10_TenBooleans_PacksBitsWithFc0F()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions { Series = XinjeSeries.XC, Station = 1, UseExtendedProtocol = false };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/xinje.md §B.1.1 "XC M10 = 0x000A, 다중 코일 쓰기 FC 0x0F"
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] responsePdu = [0x0F, sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4]];
            return ModbusRequestBuilder.BuildRtuAdu(1, responsePdu);
        });

        // 10 bits: [1, 0, 1, 1, 0, 0, 0, 1] -> 0x8D, [1, 1] -> 0x03
        byte[] booleanBytes = [1, 0, 1, 1, 0, 0, 0, 1, 1, 1];
        var request = new PlcWriteRequest("M10", PlcDataType.Boolean, booleanBytes);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x0F, 0x00, 0x0A, 0x00, 0x0A, 0x02, 0x8D, 0x03 }, sentPdu);
    }

    [Fact]
    public async Task WriteAsync_Extended_M10_TenBooleans_PacksBitsWithFc1F()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions { Series = XinjeSeries.XdXl, Station = 1, UseExtendedProtocol = true };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // 출처: docs/plc-packet-manual/xinje.md §A.1.3 & §A.2 "연속 비트 쓰기 0x1F 및 선두 7바이트 PDU 에코"
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] echoPdu = [sentPdu[0], sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4], sentPdu[5], sentPdu[6]];
            return ModbusRequestBuilder.BuildRtuAdu(1, echoPdu);
        });

        byte[] booleanBytes = [1, 0, 1, 1, 0, 0, 0, 1, 1, 1];
        var request = new PlcWriteRequest("M10", PlcDataType.Boolean, booleanBytes);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x1F, 0x03, 0x00, 0x00, 0x0A, 0x00, 0x0A, 0x02, 0x8D, 0x03 }, sentPdu);
    }

    [Fact]
    public async Task ReadAsync_Extended_D100_MultiElementInt32_ChunksByWordsPerElement()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions
        {
            Series = XinjeSeries.XdXl,
            Station = 1,
            UseExtendedProtocol = true,
            MaxReadWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // 청크 1: D100(0x000064)에서 4워드(8바이트)
        byte[] chunk1Pdu = [0x20, 0x08, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk1Pdu));

        // 청크 2: D104(0x000068)에서 2워드(4바이트)
        byte[] chunk2Pdu = [0x20, 0x04, 0x09, 0x0A, 0x0B, 0x0C];
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, chunk2Pdu));

        var request = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C }, result);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(new byte[] { 0x20, 0x80, 0x00, 0x00, 0x64, 0x00, 0x04 }, sentPdu1);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(new byte[] { 0x20, 0x80, 0x00, 0x00, 0x68, 0x00, 0x02 }, sentPdu2);
    }

    [Fact]
    public async Task WriteAsync_Extended_D100_MultiElementInt32_SecondChunkFails_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions
        {
            Series = XinjeSeries.XdXl,
            Station = 1,
            UseExtendedProtocol = true,
            MaxWriteWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // 첫 번째 청크(Int32 2개 = 4워드) 정상 7바이트 에코 응답
        transport.EnqueueResponse(frame =>
        {
            byte[] sentPdu = ModbusResponseParser.UnwrapRtuAdu(frame, 1);
            byte[] echoPdu = [sentPdu[0], sentPdu[1], sentPdu[2], sentPdu[3], sentPdu[4], sentPdu[5], sentPdu[6]];
            return ModbusRequestBuilder.BuildRtuAdu(1, echoPdu);
        });

        // 두 번째 청크(Int32 1개 = 2워드) 장치 예외 응답 (0xA1, 0x02)
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, [0xA1, 0x02]));

        byte[] payload = new byte[12];
        var request = new PlcWriteRequest("D100", PlcDataType.Int32, payload);

        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));
        Assert.Equal(2, exception.CompletedCount);

        Assert.Equal(2, transport.SentFrames.Count);
        byte[] sentPdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(0x64, sentPdu1[4]);
        Assert.Equal(0x04, sentPdu1[6]);

        byte[] sentPdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(0x68, sentPdu2[4]);
        Assert.Equal(0x02, sentPdu2[6]);
    }

    [Fact]
    public async Task WriteAsync_StandardModbus_D100_MultiElementInt32_SecondChunkFails_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions
        {
            Series = XinjeSeries.XC,
            Station = 1,
            UseExtendedProtocol = false,
            MaxWriteWords = 4, // Int32(2워드) 기준 청크당 최대 2개 원소
        };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

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
    }

    [Theory]
    [InlineData(new byte[] { 0x21, 0x80, 0x00, 0x00, 0x64, 0x00 })] // 길이 부족 (6바이트)
    [InlineData(new byte[] { 0x21, 0x80, 0x00, 0x00, 0x64, 0x00, 0x01, 0x00 })] // 길이 초과 (8바이트)
    [InlineData(new byte[] { 0x21, 0x81, 0x00, 0x00, 0x64, 0x00, 0x01 })] // 디바이스 코드 불일치 (0x81 != 0x80)
    [InlineData(new byte[] { 0x21, 0x80, 0x00, 0x00, 0x65, 0x00, 0x01 })] // 24비트 시작 주소 불일치 (0x65 != 0x64)
    [InlineData(new byte[] { 0x21, 0x80, 0x00, 0x00, 0x64, 0x00, 0x02 })] // 수량 에코 불일치 (2 != 1)
    public void VerifyExtendedWriteResponse_LengthOrEchoMismatch_ThrowsInvalidResponse(byte[] invalidResponsePdu)
    {
        var expectedAddress = XinjeAddress.Parse("D100", XinjeSeries.XdXl, isBitAccess: false);

        PlcException exception = Assert.Throws<PlcException>(() =>
            XinjeResponseParser.VerifyExtendedWriteResponse(invalidResponsePdu, 0x21, expectedAddress, expectedCount: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void ParseExtendedReadResponse_ByteCountMismatch_ThrowsInvalidResponse()
    {
        // 요청은 2워드(4바이트)인데 응답이 1워드(2바이트)를 선언한 경우
        byte[] mismatchedResponsePdu = [0x20, 0x02, 0x12, 0x34];

        PlcException exception = Assert.Throws<PlcException>(() =>
            XinjeResponseParser.ParseExtendedReadResponse(mismatchedResponsePdu, 0x20, expectedByteCount: 4));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Theory]
    [InlineData("X-1")]
    [InlineData("X8")]
    [InlineData("Y18")]
    [InlineData("D16777216")]
    public void Address_TryParse_InvalidOctalOrOutOfRange_ReturnsFalse(string invalidAddress)
    {
        Assert.False(XinjeAddress.TryParse(invalidAddress, XinjeSeries.XdXl, isBitAccess: false, out _, out _));
    }

    [Fact]
    public async Task StandardModbus_XC_CrossingDiscontinuousBoundary_ThrowsArgumentException()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions { Series = XinjeSeries.XC, UseExtendedProtocol = false };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // XC 시리즈 D7999(0x1F3F) -> D8000(0x4000) 불연속 경계 침범 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D7999", PlcDataType.Int16, 2), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("D7999", PlcDataType.Int16, new byte[4]), default));

        // XC 시리즈 M7999(0x1F3F) -> M8000(0x6000) 불연속 경계 침범 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("M7999", PlcDataType.Boolean, 2), default));
    }
}
