using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Panasonic;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Panasonic;

public class MewtocolProtocolTests
{
    private static byte[] MakeResponse(byte station, char status, string body, char header = '%')
    {
        string stationText = station == 0xEE
            ? "EE"
            : station.ToString("D2", CultureInfo.InvariantCulture);
        string prefix = $"{header}{stationText}{status}{body}";
        byte bcc = 0;
        foreach (char character in prefix)
        {
            bcc ^= (byte)character;
        }

        string fullFrame = $"{prefix}{bcc:X2}\r";
        return Encoding.ASCII.GetBytes(fullFrame);
    }

    [Fact]
    public async Task ReadAsync_X0_SingleCoil_MatchesSpecificationVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // Mock response: %01$RC115\r (Bit 1 ON)
        byte[] response = MakeResponse(1, '$', "RC1");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        // Exact vector from §4: %01#RCSX00001D\r
        Assert.Equal("%01#RCSX00001D\r", sent);
    }

    [Theory]
    [InlineData((byte)10, "%10")]
    [InlineData((byte)16, "%16")]
    [InlineData((byte)99, "%99")]
    [InlineData((byte)0xEE, "%EE")]
    public async Task ReadAsync_StationEncodingAndDecoding_UsesDecimalForStationsAndEeForDirect(byte station, string expectedPrefix)
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = station });
        transport.EnqueueResponse(MakeResponse(station, '$', "RD3412"));

        var request = new PlcReadRequest("DT0", PlcDataType.UInt16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith($"{expectedPrefix}#RDD0000000000", sent);
    }

    [Fact]
    public void ParseReadWords_HexStationMismatch_RejectsStation10WhenExpecting16()
    {
        byte[] responseFromStation10 = MakeResponse(10, '$', "RD3412");
        var exception = Assert.Throws<PlcException>(
            () => MewtocolResponseParser.ParseReadWords(responseFromStation10, expectedStation: 16, expectedWordCount: 1));
        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("1A")]
    [InlineData("ee")]
    public void ParseReadSingleBit_InvalidStationText_ThrowsInvalidResponse(string invalidStation)
    {
        string prefix = $"%{invalidStation}$RC1";
        byte bcc = 0;
        foreach (char character in prefix)
        {
            bcc ^= (byte)character;
        }

        byte[] frame = Encoding.ASCII.GetBytes($"{prefix}{bcc:X2}\r");
        var exception = Assert.Throws<PlcException>(() => MewtocolResponseParser.ParseReadSingleBit(frame, expectedStation: 1));
        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void BuildWriteWords_StandardVsExpandedHeader_SwitchesAt24Words()
    {
        var address = MewtocolAddress.Parse("DT0");
        byte[] frame24 = MewtocolRequestBuilder.BuildWriteWords(0xEE, address, 24, new byte[48]);
        byte[] frame25 = MewtocolRequestBuilder.BuildWriteWords(0xEE, address, 25, new byte[50]);

        Assert.Equal((byte)'%', frame24[0]);
        Assert.Equal(116, frame24.Length);
        Assert.True(frame24.Length <= 118);
        Assert.Equal((byte)'<', frame25[0]);
    }

    [Fact]
    public void ResponseParser_EnforcesExactPayloadLength()
    {
        byte[] extraBitResponse = MakeResponse(0xEE, '$', "RC10");
        Assert.Throws<PlcException>(() => MewtocolResponseParser.ParseReadSingleBit(extraBitResponse, 0xEE));

        byte[] extraWordResponse = MakeResponse(0xEE, '$', "RD3412ABCD");
        Assert.Throws<PlcException>(() => MewtocolResponseParser.ParseReadWords(extraWordResponse, 0xEE, expectedWordCount: 1));

        byte[] extraWriteResponse = MakeResponse(0xEE, '$', "WD00");
        Assert.Throws<PlcException>(() => MewtocolResponseParser.VerifyWriteResponse(extraWriteResponse, 0xEE, "WD"));
    }

    [Fact]
    public async Task WriteAsync_OverflowingEndAddressAcrossChunks_FailsBeforeSendingFirstChunk()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE, MaxWriteWords = 5 });

        var request = new PlcWriteRequest("DT99995", PlcDataType.UInt16, new byte[20]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => protocol.WriteAsync(request, default));
        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public async Task ReadAsync_MaxReadWordsSmallerThanElementSize_ThrowsArgumentOutOfRangeException()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE, MaxReadWords = 2 });

        var request = new PlcReadRequest("DT0", PlcDataType.Int64, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => protocol.ReadAsync(request, default));
        Assert.Empty(transport.SentFrames);
    }

    [Fact]
    public async Task ReadAsync_MultipleCoils_SequentiallyReadsWithRcsAcrossWordBoundary()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE });

        transport.EnqueueResponse(MakeResponse(0xEE, '$', "RC1"));
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "RC0"));
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "RC1"));

        var request = new PlcReadRequest("R10E", PlcDataType.Boolean, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 1, 0, 1 }, result);
        Assert.Equal(3, transport.SentFrames.Count);
        Assert.StartsWith("%EE#RCSR010E", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.StartsWith("%EE#RCSR010F", Encoding.ASCII.GetString(transport.SentFrames[1]));
        Assert.StartsWith("%EE#RCSR0110", Encoding.ASCII.GetString(transport.SentFrames[2]));
    }

    [Fact]
    public async Task WriteAsync_MultipleCoils_SequentiallyWritesWithWcs()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE });

        transport.EnqueueResponse(MakeResponse(0xEE, '$', "WC"));
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "WC"));
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "WC"));

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("R10E", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Equal(3, transport.SentFrames.Count);
        Assert.StartsWith("%EE#WCSR010E1", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.StartsWith("%EE#WCSR010F0", Encoding.ASCII.GetString(transport.SentFrames[1]));
        Assert.StartsWith("%EE#WCSR01101", Encoding.ASCII.GetString(transport.SentFrames[2]));
    }

    [Fact]
    public async Task WriteAsync_MultipleCoils_FailureOnSecondCoil_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE });

        transport.EnqueueResponse(MakeResponse(0xEE, '$', "WC"));
        transport.EnqueueResponse(MakeResponse(0xEE, '!', "65"));

        byte[] bitData = [1, 1, 0];
        var request = new PlcWriteRequest("R0", PlcDataType.Boolean, bitData);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));

        Assert.Equal((int)MewtocolErrorCode.ProtectionError, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    [Fact]
    public async Task ReadAsync_DT0_2Words_MatchesSpecificationVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // 2 words: 0x1234, 0xABCD in LE -> "3412ABCD"
        byte[] response = MakeResponse(1, '$', "RD3412ABCD");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("DT0", PlcDataType.Int16, 2);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);
        Assert.Equal(0xAB, result[2]);
        Assert.Equal(0xCD, result[3]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        // BCC of %01#RDD0000000001 is 54
        Assert.Equal("%01#RDD000000000154\r", sent);
    }

    [Fact]
    public async Task ReadAsync_Int32MultiChunk_ChunksAlignedByElementAndAdvancesWords()
    {
        var transport = new ScriptedTransport();
        // MaxReadWords = 5 -> wordsPerElement(Int32) = 2 -> maxElementsPerChunk = 2 (4 words per chunk)
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE, MaxReadWords = 5 });

        // Chunk 1: 2 Int32 elements (4 words = 16 hex chars)
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "RD0100020003000400"));
        // Chunk 2: 1 Int32 element (2 words = 8 hex chars)
        transport.EnqueueResponse(MakeResponse(0xEE, '$', "RD05000600"));

        var request = new PlcReadRequest("DT100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(12, result.Length);
        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0 }, result);

        Assert.Equal(2, transport.SentFrames.Count);
        Assert.StartsWith("%EE#RDD0010000103", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.StartsWith("%EE#RDD0010400105", Encoding.ASCII.GetString(transport.SentFrames[1]));
    }

    [Fact]
    public async Task WriteAsync_DT100_2Words_BuildsExpectedRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE });

        byte[] response = MakeResponse(0xEE, '$', "WD");
        transport.EnqueueResponse(response);

        byte[] data = [0x34, 0x12, 0xCD, 0xAB];
        var request = new PlcWriteRequest("DT100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("%EE#WDD00100001013412CDAB", sent);
        Assert.EndsWith("\r", sent);
    }

    [Fact]
    public async Task WriteAsync_Int32MultiChunk_FailureOnSecondChunk_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        // MaxWriteWords = 5 -> wordsPerElement(Int32) = 2 -> maxElementsPerChunk = 2 (4 words per chunk)
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 0xEE, MaxWriteWords = 5 });

        transport.EnqueueResponse(MakeResponse(0xEE, '$', "WD"));
        transport.EnqueueResponse(MakeResponse(0xEE, '!', "61"));

        byte[] data = new byte[12]; // 3 Int32 elements
        var request = new PlcWriteRequest("DT100", PlcDataType.Int32, data);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));

        Assert.Equal((int)MewtocolErrorCode.DataError, exception.DeviceErrorCode);
        Assert.Equal(2, exception.CompletedCount);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.StartsWith("%EE#WDD0010000103", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.StartsWith("%EE#WDD0010400105", Encoding.ASCII.GetString(transport.SentFrames[1]));
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new MewtocolProtocol(transport, new MewtocolOptions { Station = 1 });

        // Error code 40 (BCC error)
        byte[] response = MakeResponse(1, '!', "40");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal((int)MewtocolErrorCode.BccError, exception.DeviceErrorCode);
        Assert.Contains("BCC", exception.Message);
    }

    [Fact]
    public void MewtocolErrorCode_ContainsAll37OfficialCodes()
    {
        Array values = Enum.GetValues(typeof(MewtocolErrorCode));
        Assert.Equal(37, values.Length);

        foreach (MewtocolErrorCode code in values)
        {
            string description = code.GetDescription();
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.DoesNotContain("알 수 없는 MEWTOCOL 오류 코드", description);
        }
    }
}
