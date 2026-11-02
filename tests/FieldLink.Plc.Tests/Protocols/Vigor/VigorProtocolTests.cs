using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Vigor;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Vigor;

public class VigorProtocolTests
{
    private static byte[] MakeResponseFrame(byte station, ushort declaredBodyLength, byte statusCode, params byte[] payload)
    {
        byte[] unescapedBody = new byte[4 + payload.Length];
        unescapedBody[0] = station;
        BinaryBuffer.WriteUInt16LittleEndian(unescapedBody, 1, declaredBodyLength);
        unescapedBody[3] = statusCode;
        Buffer.BlockCopy(payload, 0, unescapedBody, 4, payload.Length);

        byte checksum = 0;
        int dleCount = 0;
        for (int i = 0; i < unescapedBody.Length; i++)
        {
            checksum = (byte)(checksum + unescapedBody[i]);
            if (unescapedBody[i] == 0x10)
            {
                dleCount++;
            }
        }

        byte[] frame = new byte[6 + unescapedBody.Length + dleCount];
        int offset = 0;
        frame[offset++] = 0x10;
        frame[offset++] = 0x02;

        for (int i = 0; i < unescapedBody.Length; i++)
        {
            frame[offset++] = unescapedBody[i];
            if (unescapedBody[i] == 0x10)
            {
                frame[offset++] = 0x10;
            }
        }

        frame[offset++] = 0x10;
        frame[offset++] = 0x03;
        BinaryBuffer.WriteAsciiHexByte(frame, offset, checksum);
        return frame;
    }

    [Fact]
    public void BuildReadRequest_D100_1Word_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/vigor.md §4 "D100 1워드 읽기"
        var parsedAddress = VigorAddress.Parse("D100");
        byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(station: 1, parsedAddress, count: 1);

        byte[] expected =
        [
            0x10, 0x02, 0x01, 0x07, 0x00, 0x20, 0xA0, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x03, 0x43, 0x41
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void BuildReadRequest_M10_8Bits_WithDleStuffing_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/vigor.md §4 "M10 8비트 읽기"
        var parsedAddress = VigorAddress.Parse("M10");
        byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(station: 1, parsedAddress, count: 8);

        byte[] expected =
        [
            0x10, 0x02, 0x01, 0x07, 0x00, 0x21, 0x92, 0x10, 0x10, 0x00, 0x00, 0x08, 0x00, 0x10, 0x03, 0x44, 0x33
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void ParseResponse_ValidReadWord_ReturnsData()
    {
        byte[] response =
        [
            0x10, 0x02, 0x01, 0x03, 0x00, 0x00, 0x34, 0x12, 0x10, 0x03, 0x34, 0x41
        ];

        byte[] data = VigorResponseParser.ParseResponse(response, expectedStation: 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void ParseResponse_BodyLengthMismatch_ThrowsInvalidResponse()
    {
        // 실제 남은 길이는 ACK(1) + 데이터(2) = 3인데 선언된 길이를 5로 설정
        byte[] corruptedFrame = MakeResponseFrame(station: 1, declaredBodyLength: 5, statusCode: 0x00, 0x34, 0x12);

        var exception = Assert.Throws<PlcException>(() =>
            VigorResponseParser.ParseResponse(corruptedFrame, expectedStation: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void ParseResponse_ErrorCode_ThrowsPlcDeviceException()
    {
        byte[] response =
        [
            0x10, 0x02, 0x01, 0x01, 0x00, 0x06, 0x10, 0x03, 0x30, 0x38
        ];

        var exception = Assert.Throws<PlcDeviceException>(() =>
            VigorResponseParser.ParseResponse(response, expectedStation: 1));

        Assert.Equal(0x06, exception.DeviceErrorCode);
        Assert.Contains("0x06", exception.Message);
    }

    [Fact]
    public async Task Protocol_ReadAsync_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new VigorProtocol(transport, new VigorOptions { StationNumber = 1 });

        transport.EnqueueResponse(new byte[]
        {
            0x10, 0x02, 0x01, 0x03, 0x00, 0x00, 0x34, 0x12, 0x10, 0x03, 0x34, 0x41
        });

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
        Assert.Single(transport.SentFrames);
    }

    [Fact]
    public async Task Protocol_WriteAsync_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new VigorProtocol(transport, new VigorOptions { StationNumber = 1 });

        transport.EnqueueResponse(new byte[]
        {
            0x10, 0x02, 0x01, 0x01, 0x00, 0x00, 0x10, 0x03, 0x30, 0x32
        });

        var request = new PlcWriteRequest("D100", PlcDataType.Int16, new byte[] { 0x34, 0x12 });
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x10, sent[0]);
        Assert.Equal(0x02, sent[1]);
        Assert.Equal(0x01, sent[2]);
    }

    [Fact]
    public async Task Protocol_ReadAndWrite_C200_32BitCounter_Uses4BytesPerPoint()
    {
        var transport = new ScriptedTransport();
        var options = new VigorOptions { StationNumber = 1, MaxWordsPerRequest = 1 };
        var protocol = new VigorProtocol(transport, options);

        // C200(1점 = 4바이트) + C201(1점 = 4바이트) 분할 읽기
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 5, statusCode: 0x00, 0x11, 0x22, 0x33, 0x44));
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 5, statusCode: 0x00, 0x55, 0x66, 0x77, 0x88));

        var readRequest = new PlcReadRequest("C200", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 }, readResult);
        Assert.Equal(2, transport.SentFrames.Count);

        // 첫 번째 청크 요청: 디바이스 코드 0xAD(C200), BCD 주소 00 02 00, 개수 1 (01 00)
        byte[] firstReadSent = transport.SentFrames[0];
        Assert.Equal(0xAD, firstReadSent[6]);
        Assert.Equal(0x00, firstReadSent[7]);
        Assert.Equal(0x02, firstReadSent[8]);
        Assert.Equal(0x00, firstReadSent[9]);
        Assert.Equal(0x01, firstReadSent[10]);
        Assert.Equal(0x00, firstReadSent[11]);

        // 두 번째 청크 요청: 디바이스 코드 0xAD(C201), BCD 주소 01 02 00, 개수 1 (01 00)
        byte[] secondReadSent = transport.SentFrames[1];
        Assert.Equal(0xAD, secondReadSent[6]);
        Assert.Equal(0x01, secondReadSent[7]);
        Assert.Equal(0x02, secondReadSent[8]);
        Assert.Equal(0x00, secondReadSent[9]);
        Assert.Equal(0x01, secondReadSent[10]);
        Assert.Equal(0x00, secondReadSent[11]);

        // C200 2점 쓰기 (청크당 1점 = 4바이트)
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 1, statusCode: 0x00));
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 1, statusCode: 0x00));

        var writeRequest = new PlcWriteRequest("C200", PlcDataType.Int32, readResult);
        await protocol.WriteAsync(writeRequest, default);

        Assert.Equal(4, transport.SentFrames.Count);
        byte[] firstWriteSent = transport.SentFrames[2];
        // 쓰기 본문 길이 = 7 + 4 = 11 (0x0B), 기능코드 0x28, 디바이스 0xAD, 카운터 개수 = 1
        Assert.Equal(0x0B, firstWriteSent[3]);
        Assert.Equal(0x28, firstWriteSent[5]);
        Assert.Equal(0xAD, firstWriteSent[6]);
        Assert.Equal(0x01, firstWriteSent[10]);
        Assert.Equal(0x00, firstWriteSent[11]);
    }

    [Fact]
    public async Task Protocol_ReadAndWrite_D100_Int32_ChunksByTwoWordsPerElementAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new VigorOptions { StationNumber = 1, MaxWordsPerRequest = 2 };
        var protocol = new VigorProtocol(transport, options);

        // D100에서 Int32 2개 읽기 (MaxWordsPerRequest=2이므로 청크당 Int32 1개 = 2워드씩 분할: D100, D102)
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 5, statusCode: 0x00, 0x01, 0x02, 0x03, 0x04));
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 5, statusCode: 0x00, 0x05, 0x06, 0x07, 0x08));

        var readRequest = new PlcReadRequest("D100", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, readResult);
        Assert.Equal(2, transport.SentFrames.Count);

        // 첫 번째 청크: D100 (BCD 00 01 00), 2워드 요청
        Assert.Equal(0x00, transport.SentFrames[0][7]);
        Assert.Equal(0x01, transport.SentFrames[0][8]);
        Assert.Equal(0x02, transport.SentFrames[0][10]);

        // 두 번째 청크: D102 (BCD 02 01 00), 2워드 요청
        Assert.Equal(0x02, transport.SentFrames[1][7]);
        Assert.Equal(0x01, transport.SentFrames[1][8]);
        Assert.Equal(0x02, transport.SentFrames[1][10]);

        // D100에 Int32 2개 분할 쓰기 중 두 번째 청크에서 장치 오류(0x06) 발생 시 CompletedCount = 1 보존
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 1, statusCode: 0x00));
        transport.EnqueueResponse(MakeResponseFrame(1, declaredBodyLength: 1, statusCode: 0x06));

        var writeRequest = new PlcWriteRequest("D100", PlcDataType.Int32, readResult);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(writeRequest, default));

        Assert.Equal(0x06, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    [Theory]
    [InlineData("X08")]
    [InlineData("Y79")]
    [InlineData("X800")]
    public void Address_Parse_InvalidOctalForXY_ThrowsFormatException(string invalidAddress)
    {
        // 출처: docs/plc-packet-manual/vigor.md §1.6 "X, Y는 8진수 주소 체계(0~7)"
        Assert.Throws<FormatException>(() => VigorAddress.Parse(invalidAddress));
    }

    [Fact]
    public void Address_Offset_X7_Plus1_AdvancesInOctalToX10()
    {
        // 출처: docs/plc-packet-manual/vigor.md §1.6 "X7 + 1점 -> X10 (BCD 0x10, 0x00, 0x00)"
        VigorAddress start = VigorAddress.Parse("X7");
        VigorAddress next = start.Offset(1);

        Assert.Equal(10, next.Number);
        Assert.Equal(new byte[] { 0x10, 0x00, 0x00 }, next.BcdAddress);
    }

    [Theory]
    [InlineData("D8999", PlcDataType.Int16, 2)]
    [InlineData("M8999", PlcDataType.Boolean, 2)]
    [InlineData("C199", PlcDataType.Int16, 2)]
    public async Task Protocol_ReadAndWrite_CrossingDeviceCodeBoundary_ThrowsArgumentException(
        string startAddress,
        PlcDataType dataType,
        int count)
    {
        var transport = new ScriptedTransport();
        var protocol = new VigorProtocol(transport);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest(startAddress, dataType, count), default));

        int byteSize = dataType == PlcDataType.Boolean ? count : count * 2;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest(startAddress, dataType, new byte[byteSize]), default));
    }
}
