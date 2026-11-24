using System;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Yokogawa;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Yokogawa;

public class YokogawaProtocolTests
{
    [Fact]
    public void BuildReadRequest_D100_1Word_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/yokogawa.md §4 "D100 1워드 읽기"
        var parsedAddress = YokogawaAddress.Parse("D100");
        byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(cpuNumber: 1, parsedAddress, count: 1);

        byte[] expected =
        [
            0x11, 0x01, 0x00, 0x08, 0x00, 0x04, 0x00, 0x00, 0x00, 0x64, 0x00, 0x01
        ];

        Assert.Equal(expected, requestFrame);
    }

    [Fact]
    public void ParseReadResponse_ValidWordRead_ReturnsData()
    {
        byte[] response = [0x91, 0x00, 0x00, 0x02, 0x34, 0x12];
        byte[] data = YokogawaResponseParser.ParseReadResponse(response, expectedCommand: 0x11);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void ParseReadResponse_TrailingExtraBytes_ThrowsInvalidResponse()
    {
        // 선언된 크기(0x0002)보다 실제 본문이 1바이트 더 긴 손상 프레임 (frame.Length = 7 != 4 + 2)
        byte[] corruptedFrame = [0x91, 0x00, 0x00, 0x02, 0x34, 0x12, 0xFF];

        var exception = Assert.Throws<PlcException>(() =>
            YokogawaResponseParser.ParseReadResponse(corruptedFrame, expectedCommand: 0x11));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void ParseReadResponse_DeviceError_ThrowsPlcDeviceException()
    {
        byte[] response = [0x91, 0x03, 0x00, 0x00];

        var exception = Assert.Throws<PlcDeviceException>(() =>
            YokogawaResponseParser.ParseReadResponse(response, expectedCommand: 0x11));

        Assert.Equal(0x03, exception.DeviceErrorCode);
        Assert.Contains("ER03", exception.Message);
    }

    [Fact]
    public async Task Protocol_ReadWord_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new YokogawaProtocol(transport, new YokogawaOptions { CpuNumber = 1 });

        byte[] response = [0x91, 0x00, 0x00, 0x02, 0x34, 0x12];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x11, sent[0]);
    }

    [Fact]
    public async Task Protocol_WriteWord_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new YokogawaProtocol(transport, new YokogawaOptions { CpuNumber = 1 });

        byte[] response = [0x92, 0x00, 0x00, 0x00];
        transport.EnqueueResponse(response);

        var request = new PlcWriteRequest("D100", PlcDataType.Int16, new byte[] { 0x34, 0x12 });
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x12, sent[0]);
    }

    [Fact]
    public async Task Protocol_ReadAndWrite_Int32_ChunksProperlyAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        // MaxWordsPerRequest = 3일 때 Int32(2워드)는 청크당 1개(2워드)씩 정렬 분할되어야 함
        var options = new YokogawaOptions { CpuNumber = 1, MaxWordsPerRequest = 3 };
        var protocol = new YokogawaProtocol(transport, options);

        transport.EnqueueResponse([0x91, 0x00, 0x00, 0x04, 0x01, 0x02, 0x03, 0x04]);
        transport.EnqueueResponse([0x91, 0x00, 0x00, 0x04, 0x05, 0x06, 0x07, 0x08]);

        var readRequest = new PlcReadRequest("D100", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, readResult);
        Assert.Equal(2, transport.SentFrames.Count);

        // 첫 번째 청크: D100 (0x64), 2워드 요청
        Assert.Equal(100, transport.SentFrames[0][9]);
        Assert.Equal(2, transport.SentFrames[0][11]);

        // 두 번째 청크: D102 (0x66), 2워드 요청
        Assert.Equal(102, transport.SentFrames[1][9]);
        Assert.Equal(2, transport.SentFrames[1][11]);

        // 분할 쓰기 중 두 번째 청크에서 ER03 오류 발생 시 CompletedCount = 1 보존
        transport.EnqueueResponse([0x92, 0x00, 0x00, 0x00]);
        transport.EnqueueResponse([0x92, 0x03, 0x00, 0x00]);

        var writeRequest = new PlcWriteRequest("D100", PlcDataType.Int32, readResult);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(writeRequest, default));

        Assert.Equal(0x03, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    [Fact]
    public async Task Protocol_DataTypeMismatch_ThrowsArgumentException()
    {
        var transport = new ScriptedTransport();
        var protocol = new YokogawaProtocol(transport);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("M1", PlcDataType.Int16, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Boolean, 1), default));
    }

    [Fact]
    public void YokogawaOptions_And_Decoder_EnforceLimits()
    {
        var valid = new YokogawaOptions { CpuNumber = 4, MaxWordsPerRequest = 32763, MaxBitsPerRequest = 65527 };
        Assert.Equal((byte)4, valid.CpuNumber);
        Assert.Equal(32763, valid.MaxWordsPerRequest);
        Assert.Equal(65527, valid.MaxBitsPerRequest);

        Assert.Throws<ArgumentOutOfRangeException>(() => new YokogawaOptions { CpuNumber = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new YokogawaOptions { CpuNumber = 5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new YokogawaOptions { MaxWordsPerRequest = 32764 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new YokogawaOptions { MaxBitsPerRequest = 65528 });

        var decoder = new YokogawaFrameDecoder();
        byte[] frame65536 = new byte[65536];
        frame65536[0] = 0x91;
        frame65536[2] = 0xFF;
        frame65536[3] = 0xFC; // 65532 payload + 4 header = 65536
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame65536), out int length));
        Assert.Equal(65536, length);
    }

    [Fact]
    public void RequestBuilder_ValidatesCpuAddressBitFlagAndBitValues()
    {
        var wordAddress = YokogawaAddress.Parse("D100");
        var bitAddress = YokogawaAddress.Parse("Y10");

        Assert.Throws<ArgumentOutOfRangeException>(() => YokogawaRequestBuilder.BuildReadRequest(0, wordAddress, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => YokogawaRequestBuilder.BuildReadRequest(5, wordAddress, 1));
        Assert.Throws<ArgumentNullException>(() => YokogawaRequestBuilder.BuildWriteWordRequest(1, null!, 1, [0x01, 0x00]));
        Assert.Throws<ArgumentNullException>(() => YokogawaRequestBuilder.BuildWriteBitRequest(1, null!, 1, [0x01]));
        Assert.Throws<ArgumentException>(() => YokogawaRequestBuilder.BuildWriteWordRequest(1, bitAddress, 1, [0x01, 0x00]));
        Assert.Throws<ArgumentException>(() => YokogawaRequestBuilder.BuildWriteBitRequest(1, wordAddress, 1, [0x01]));
        Assert.Throws<ArgumentOutOfRangeException>(() => YokogawaRequestBuilder.BuildWriteBitRequest(1, bitAddress, 1, [0x02]));
        Assert.Throws<ArgumentOutOfRangeException>(() => YokogawaRequestBuilder.BuildWriteWordRequest(1, wordAddress, 32764, new byte[32764 * 2]));
    }

    [Fact]
    public void ResponseParser_SurfacesDetailErrorCodeAndRejectsNonEmptyWritePayload()
    {
        byte[] errorWithDetail = [0x91, 0x03, 0x00, 0x02, 0x01, 0x82];
        var deviceException = Assert.Throws<PlcDeviceException>(() =>
            YokogawaResponseParser.ParseReadResponse(errorWithDetail, expectedCommand: 0x11));

        Assert.Equal(0x03, deviceException.DeviceErrorCode);
        Assert.Contains("0x82", deviceException.Message);
        Assert.Contains("CPU=1", deviceException.Message);
        Assert.Contains("디바이스 액세스 오류", deviceException.Message);

        byte[] invalidWriteResponse = [0x92, 0x00, 0x00, 0x02, 0x00, 0x00];
        var writeException = Assert.Throws<PlcException>(() =>
            YokogawaResponseParser.VerifyWriteResponse(invalidWriteResponse, expectedCommand: 0x12));
        Assert.Equal(PlcErrorKind.InvalidResponse, writeException.Kind);
    }

    [Fact]
    public async Task Protocol_WordLimitSmallerThanElementSizeOrAddressOverflow_ThrowsBeforeTransport()
    {
        var transport = new ScriptedTransport();
        var smallProtocol = new YokogawaProtocol(transport, new YokogawaOptions { CpuNumber = 1, MaxWordsPerRequest = 2 });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => smallProtocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Int64, 1), default));

        var defaultProtocol = new YokogawaProtocol(transport);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => defaultProtocol.ReadAsync(new PlcReadRequest("D2147483647", PlcDataType.UInt16, 2), default));

        Assert.Empty(transport.SentFrames);
    }
}
