using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Keyence;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Keyence;

public class KeyenceHostLinkProtocolTests
{
    [Fact]
    public async Task ConnectAsync_SendsCR_ValidatesCCResponse()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("CC\r\n"));
        await protocol.ConnectAsync(default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("CR\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task ReadAsync_DM100_1Word_BuildsRDSAndParsesDecimal()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("4660\r\n"));

        var request = new PlcReadRequest("DM100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        Assert.Equal("RDS DM100 1\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task WriteAsync_DM100_2Words_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/keyence.md §A.3 "DM100 2워드 쓰기"
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));

        byte[] data = [0x34, 0x12, 0xCD, 0xAB];
        var request = new PlcWriteRequest("DM100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("WRS DM100 2 4660 43981\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task WriteAsync_MR100_3Bits_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/keyence.md §A.3 "MR100 3비트 쓰기"
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("MR100", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        Assert.Equal("WRS MR100 3 1 0 1\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        transport.EnqueueResponse(Encoding.ASCII.GetBytes("E0\r\n"));

        var request = new PlcReadRequest("DM100", PlcDataType.Int16, 1);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal((int)KeyenceHostLinkErrorCode.DeviceNumberError, exception.DeviceErrorCode);
        Assert.Contains("디바이스 번호 이상", exception.Message);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Int32_ChunksByTwoWordsPerElementAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        // MaxWordsPerRequest = 3일 때 Int32(2워드)는 청크당 1개(2워드)씩 정렬 분할되어야 함
        var options = new KeyenceHostLinkOptions { MaxWordsPerRequest = 3 };
        var protocol = new KeyenceHostLinkProtocol(transport, options);

        // DM100에서 Int32 2개 읽기 -> RDS DM100 2, RDS DM102 2
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("1 2\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("3 4\r\n"));

        var readRequest = new PlcReadRequest("DM100", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04, 0x00 }, readResult);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.Equal("RDS DM100 2\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal("RDS DM102 2\r", Encoding.ASCII.GetString(transport.SentFrames[1]));

        // DM100에 Int32 2개 분할 쓰기 중 두 번째 청크에서 E4(쓰기 금지) 발생 시 CompletedCount = 1 보존
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("E4\r\n"));

        var writeRequest = new PlcWriteRequest("DM100", PlcDataType.Int32, readResult);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(writeRequest, default));

        Assert.Equal((int)KeyenceHostLinkErrorCode.WriteProtected, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
        Assert.Equal(4, transport.SentFrames.Count);
        Assert.Equal("WRS DM100 2 1 2\r", Encoding.ASCII.GetString(transport.SentFrames[2]));
        Assert.Equal("WRS DM102 2 3 4\r", Encoding.ASCII.GetString(transport.SentFrames[3]));
    }

    [Fact]
    public async Task ReadAndWriteAsync_32BitDevices_Z1AndTS0_FormatsDWordsAndAllowsNon4ByteReads()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport);

        // Z1에 Int16 읽기/쓰기 및 홀수 바이트 쓰기는 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("Z1", PlcDataType.Int16, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("Z1", PlcDataType.Int16, new byte[] { 0x01, 0x00 }), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("Z1", PlcDataType.Byte, new byte[] { 0x01, 0x02 }), default));

        // T0 복합 디바이스 직접 쓰기 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("T0", PlcDataType.Int32, new byte[4]), default));

        // Z1 및 TS0 32비트 읽기/쓰기 + 비4바이트 배수 Byte/String 읽기 허용 검증
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("100000\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("1144201745 1732584193\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("OK\r\n"));

        byte[] readZ1 = await protocol.ReadAsync(new PlcReadRequest("Z1", PlcDataType.Int32, 1), default);
        Assert.Equal(BitConverter.GetBytes(100000), readZ1);

        byte[] readBytes = await protocol.ReadAsync(new PlcReadRequest("Z1", PlcDataType.Byte, 5), default);
        Assert.Equal(5, readBytes.Length);

        await protocol.WriteAsync(new PlcWriteRequest("Z1", PlcDataType.Int32, BitConverter.GetBytes(100000)), default);
        await protocol.WriteAsync(new PlcWriteRequest("TS0", PlcDataType.UInt32, BitConverter.GetBytes(5000u)), default);

        Assert.Equal(4, transport.SentFrames.Count);
        Assert.Equal("RDS Z1 1\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal("RDS Z1 2\r", Encoding.ASCII.GetString(transport.SentFrames[1]));
        Assert.Equal("WRS Z1 1 100000\r", Encoding.ASCII.GetString(transport.SentFrames[2]));
        Assert.Equal("WRS TS0 1 5000\r", Encoding.ASCII.GetString(transport.SentFrames[3]));
    }

    [Fact]
    public async Task ReadAsync_TcExceedingDeviceChunkLimit64_ClampsTo64AndSplits()
    {
        var transport = new ScriptedTransport();
        var protocol = new KeyenceHostLinkProtocol(transport, new KeyenceHostLinkOptions { MaxReadElements = 256 });

        string[] sixtyFourValues = new string[64];
        for (int i = 0; i < 64; i++)
        {
            sixtyFourValues[i] = (i + 1).ToString();
        }

        transport.EnqueueResponse(Encoding.ASCII.GetBytes(string.Join(" ", sixtyFourValues) + "\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("65\r\n"));

        byte[] result = await protocol.ReadAsync(new PlcReadRequest("TC0", PlcDataType.UInt32, 65), default);

        Assert.Equal(65 * 4, result.Length);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.Equal("RDS TC0 64\r", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal("RDS TC64 1\r", Encoding.ASCII.GetString(transport.SentFrames[1]));
    }

    [Fact]
    public void ResponseParser_MissingCrLfOrExtraTokens_ThrowsInvalidResponse()
    {
        Assert.Throws<PlcException>(() =>
            KeyenceHostLinkResponseParser.VerifyWriteResponse(Encoding.ASCII.GetBytes("OK\n")));
        Assert.Throws<PlcException>(() =>
            KeyenceHostLinkResponseParser.VerifyWriteResponse(Encoding.ASCII.GetBytes("OK\r")));
        Assert.Throws<PlcException>(() =>
            KeyenceHostLinkResponseParser.ParseReadWords(Encoding.ASCII.GetBytes("1 2\r\n"), expectedCount: 1));
        Assert.Throws<PlcException>(() =>
            KeyenceHostLinkResponseParser.ParseReadDWords(Encoding.ASCII.GetBytes("1 2\r\n"), expectedCount: 1));
        Assert.Throws<PlcException>(() =>
            KeyenceHostLinkResponseParser.ParseReadBits(Encoding.ASCII.GetBytes("1 0\r\n"), expectedCount: 1));
    }

    [Fact]
    public void KeyenceHostLinkOptions_And_Decoder_EnforceLimitsAndInitOrder()
    {
        var order1 = new KeyenceHostLinkOptions { MaxReadElements = 10, MaxWordsPerRequest = 50 };
        var order2 = new KeyenceHostLinkOptions { MaxWordsPerRequest = 50, MaxReadElements = 10 };

        Assert.Equal(10, order1.MaxReadElements);
        Assert.Equal(50, order1.MaxWriteElements);
        Assert.Equal(10, order2.MaxReadElements);
        Assert.Equal(50, order2.MaxWriteElements);

        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyenceHostLinkOptions { MaxWordsPerRequest = 1001 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyenceHostLinkOptions { MaxReadElements = 1001 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyenceHostLinkOptions { MaxWriteElements = 1001 });

        var decoder = new KeyenceHostLinkFrameDecoder();
        byte[] largeResponse = new byte[16384];
        Array.Fill(largeResponse, (byte)'1');
        largeResponse[16382] = (byte)'\r';
        largeResponse[16383] = (byte)'\n';
        bool hasFrame = decoder.TryGetFrameLength(new ArraySegment<byte>(largeResponse), out int frameLength);
        Assert.True(hasFrame);
        Assert.Equal(16384, frameLength);
    }

    [Fact]
    public void RequestBuilder_RejectsZeroCountAndMismatchedBitLength()
    {
        var wordAddress = KeyenceHostLinkAddress.Parse("DM100");
        var dwordAddress = KeyenceHostLinkAddress.Parse("Z1");
        var bitAddress = KeyenceHostLinkAddress.Parse("MR100");

        Assert.Throws<ArgumentOutOfRangeException>(() => KeyenceHostLinkRequestBuilder.BuildWriteDWords(dwordAddress, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyenceHostLinkRequestBuilder.BuildWriteBits(bitAddress, 0, []));
        Assert.Throws<ArgumentException>(() => KeyenceHostLinkRequestBuilder.BuildWriteBits(bitAddress, 1, [1, 0]));
        Assert.Throws<ArgumentException>(() => KeyenceHostLinkRequestBuilder.BuildWriteWords(bitAddress, 1, [1, 0]));
        Assert.Throws<ArgumentException>(() => KeyenceHostLinkRequestBuilder.BuildWriteBits(wordAddress, 1, [1]));
    }

    [Fact]
    public async Task ReadAsync_LimitSmallerThanElementSizeOrExceedingBounds_ThrowsBeforeTransport()
    {
        var transport = new ScriptedTransport();
        var smallProtocol = new KeyenceHostLinkProtocol(transport, new KeyenceHostLinkOptions { MaxReadElements = 1 });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => smallProtocol.ReadAsync(new PlcReadRequest("Z1", PlcDataType.Int64, 1), default));

        var defaultProtocol = new KeyenceHostLinkProtocol(transport);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => defaultProtocol.ReadAsync(new PlcReadRequest("Z1", PlcDataType.Int32, 13), default));

        Assert.Empty(transport.SentFrames);
    }
}
