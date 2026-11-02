using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.FujiElectric;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.FujiElectric;

public class FujiElectricProtocolTests
{
    [Fact]
    public async Task ReadAsync_Spb_D100_1Word_BuildsCorrectAsciiAndDecodesData()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        // 출처: docs/plc-packet-manual/fuji.md §A.3 "D100 1워드 읽기" (:0109FFFF00000C00010100\r\n)
        // 응답 본문: FFFF(2) + 00(1) + 0000(2) + 3412(2) = 7바이트(07)
        byte[] response = Encoding.ASCII.GetBytes(":0107FFFF0000003412\r\n");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":0109FFFF00000C00010100\r\n", sent);
    }

    [Fact]
    public async Task ReadAsync_Spb_WithLrc_ParsesSuccessfully()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        byte[] response = Encoding.ASCII.GetBytes(":0107FFFF000000341236\r\n");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);
    }

    [Fact]
    public void ParseReadWords_Spb_LrcMismatch_ThrowsPlcException()
    {
        byte[] response = Encoding.ASCII.GetBytes(":0107FFFF000000341200\r\n");

        PlcException exception = Assert.Throws<PlcException>(() =>
            FujiElectricSpbResponseParser.ParseReadWords(response, expectedStation: 1, expectedCount: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
        Assert.Contains("LRC 체크섬 불일치", exception.Message);
    }

    [Fact]
    public void ParseReadWords_Spb_DeclaredLengthMismatchWithoutLrc_ThrowsPlcException()
    {
        // 실제 본문은 7바이트(FFFF + 00 + 0000 + 3412)인데 헤더 [3..5]에 04로 잘못 선언된 표준 프레임
        byte[] response = Encoding.ASCII.GetBytes(":0104FFFF0000003412\r\n");

        PlcException exception = Assert.Throws<PlcException>(() =>
            FujiElectricSpbResponseParser.ParseReadWords(response, expectedStation: 1, expectedCount: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void ParseReadWords_Spb_DeclaredLengthMismatchWithLrc_ThrowsPlcException()
    {
        // 실제 본문은 7바이트인데 헤더 [3..5]에 08로 잘못 선언된 LRC 포함 프레임
        byte[] response = Encoding.ASCII.GetBytes(":0108FFFF000000341235\r\n");

        PlcException exception = Assert.Throws<PlcException>(() =>
            FujiElectricSpbResponseParser.ParseReadWords(response, expectedStation: 1, expectedCount: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task WriteAsync_Spb_D100_2Words_BuildsExpectedRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        // 쓰기 응답 본문: FFFF(2) + 00(1) + 0100(2) = 5바이트(05)
        byte[] response = Encoding.ASCII.GetBytes(":0105FFFF000100\r\n");
        transport.EnqueueResponse(response);

        // 출처: docs/plc-packet-manual/fuji.md §A.3 "D100 2워드 쓰기" (:010DFFFF01000C000102003412ABCD\r\n)
        byte[] data = [0x34, 0x12, 0xAB, 0xCD];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":010DFFFF01000C000102003412ABCD\r\n", sent);
    }

    [Fact]
    public async Task WriteAsync_Spb_SingleBit_BuildsBitWriteCommand()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        byte[] response = Encoding.ASCII.GetBytes(":0105FFFF000102\r\n");
        transport.EnqueueResponse(response);

        // 출처: docs/plc-packet-manual/fuji.md §A.3 "M1000 비트 ON" (:0109FFFF01020225010001\r\n)
        byte[] bitData = [1];
        var request = new PlcWriteRequest("M1000", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":0109FFFF01020225010001\r\n", sent);
    }

    [Fact]
    public async Task ReadAsync_Spb_MultiBitAcrossWords_M100_20Bits_ExtractsBitsAccurately()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        // M100(워드 6, 비트 4)부터 20비트(M100~M119) -> 워드 6, 워드 7 총 2워드 읽기
        // 워드 6: 비트 4(M100), 비트 15(M111) ON -> 0x8010 -> LE 바이트 10 80
        // 워드 7: 비트 3(M115), 비트 7(M119) ON -> 0x0088 -> LE 바이트 88 00
        byte[] response = Encoding.ASCII.GetBytes(":0109FFFF00000010808800\r\n");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M100", PlcDataType.Boolean, 20);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sent = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.Equal(":0109FFFF00000206000200\r\n", sent);

        Assert.Equal(20, result.Length);
        Assert.Equal(1, result[0]);  // M100 (word 6, bit 4)
        Assert.Equal(0, result[1]);  // M101
        Assert.Equal(1, result[11]); // M111 (word 6, bit 15)
        Assert.Equal(0, result[12]); // M112 (word 7, bit 0)
        Assert.Equal(1, result[15]); // M115 (word 7, bit 3)
        Assert.Equal(1, result[19]); // M119 (word 7, bit 7)
    }

    [Fact]
    public async Task WriteAsync_Spb_MultiBitAcrossWords_M100_20Bits_SendsSingleBitWritePerPoint()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSpbProtocol(transport, new FujiElectricSpbOptions { Station = 1 });

        byte[] bitValues = new byte[20];
        bitValues[0] = 1;  // M100: word 6, bit 4 -> byte 12("1200"), bit 04, ON("01")
        bitValues[12] = 1; // M112: word 7, bit 0 -> byte 14("1400"), bit 00, ON("01")
        bitValues[19] = 1; // M119: word 7, bit 7 -> byte 14("1400"), bit 07, ON("01")

        for (int i = 0; i < 20; i++)
        {
            transport.EnqueueResponse(Encoding.ASCII.GetBytes(":0105FFFF000102\r\n"));
        }

        var request = new PlcWriteRequest("M100", PlcDataType.Boolean, bitValues);
        await protocol.WriteAsync(request, default);

        Assert.Equal(20, transport.SentFrames.Count);
        Assert.Equal(":0109FFFF01020212000401\r\n", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal(":0109FFFF01020212000500\r\n", Encoding.ASCII.GetString(transport.SentFrames[1]));
        Assert.Equal(":0109FFFF01020214000001\r\n", Encoding.ASCII.GetString(transport.SentFrames[12]));
        Assert.Equal(":0109FFFF01020214000701\r\n", Encoding.ASCII.GetString(transport.SentFrames[19]));
    }

    [Fact]
    public async Task ReadAsync_And_WriteAsync_Spb_Int32_ChunksByMultiWordElementAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new FujiElectricSpbOptions { Station = 1, MaxReadWords = 4, MaxWriteWords = 4 };
        var protocol = new FujiElectricSpbProtocol(transport, options);

        // Int32 3개(총 6워드) 읽기 -> 1청크: D100 4워드(Int32 2개), 2청크: D104 2워드(Int32 1개)
        transport.EnqueueResponse(Encoding.ASCII.GetBytes(":010DFFFF0000000100020003000400\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes(":0109FFFF00000005000600\r\n"));

        var readRequest = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] readBytes = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(12, readBytes.Length);
        Assert.Equal(":0109FFFF00000C00010400\r\n", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.Equal(":0109FFFF00000C04010200\r\n", Encoding.ASCII.GetString(transport.SentFrames[1]));

        // Int32 3개(총 6워드) 쓰기 중 2번째 청크에서 장치 오류(07) 발생 -> CompletedCount == 2 검증
        transport.EnqueueResponse(Encoding.ASCII.GetBytes(":0105FFFF000100\r\n"));
        transport.EnqueueResponse(Encoding.ASCII.GetBytes(":0105FFFF070100\r\n"));

        var writeRequest = new PlcWriteRequest("D100", PlcDataType.Int32, readBytes);
        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(
            () => protocol.WriteAsync(writeRequest, default));

        Assert.Equal(2, exception.CompletedCount);
        Assert.Equal(":0111FFFF01000C000104000100020003000400\r\n", Encoding.ASCII.GetString(transport.SentFrames[2]));
        Assert.Equal(":010DFFFF01000C0401020005000600\r\n", Encoding.ASCII.GetString(transport.SentFrames[3]));
    }

    [Fact]
    public async Task ReadAsync_Sph_M1_100_1Word_MatchesSpecificationTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSphProtocol(transport, new FujiElectricSphOptions { ConnectionId = 1 });

        byte[] response = BuildSphReadResponse(connectionId: 1, areaCode: 0x02, startWord: 100, [0x34, 0x12]);
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M1.100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];

        // 출처: docs/plc-packet-manual/fuji.md §C.3 "M1.100 1워드 읽기, 연결 ID 1"
        byte[] expectedVector =
        [
            0xFB, 0x80, 0x80, 0x00, 0xFF, 0x7B, 0x01, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x06, 0x00,
            0x02, 0x64, 0x00, 0x00, 0x01, 0x00,
        ];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task ReadAsync_Sph_MultiBitAcrossWords_M1_10_0_20Bits_ExtractsBitsAccurately()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSphProtocol(transport, new FujiElectricSphOptions { ConnectionId = 1 });

        // M1.10.0부터 20비트 -> 워드 10, 워드 11 총 2워드 읽기
        // 워드 10: 비트 0, 비트 15 ON (0x8001 -> LE: 01 80)
        // 워드 11: 비트 3(전체 19번째 비트) ON (0x0008 -> LE: 08 00)
        byte[] response = BuildSphReadResponse(connectionId: 1, areaCode: 0x02, startWord: 10, [0x01, 0x80, 0x08, 0x00]);
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M1.10.0", PlcDataType.Boolean, 20);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x02, sent[20]); // M1
        Assert.Equal(10, sent[21]);   // startWord = 10
        Assert.Equal(2, sent[24]);    // wordCount = 2

        Assert.Equal(20, result.Length);
        Assert.Equal(1, result[0]);  // word 10, bit 0
        Assert.Equal(1, result[15]); // word 10, bit 15
        Assert.Equal(0, result[16]); // word 11, bit 0
        Assert.Equal(1, result[19]); // word 11, bit 3
    }

    [Fact]
    public async Task WriteAsync_Sph_MultiBitAcrossWords_M1_10_0_20Bits_PerformsReadModifyWrite()
    {
        var transport = new ScriptedTransport();
        var protocol = new FujiElectricSphProtocol(transport, new FujiElectricSphOptions { ConnectionId = 1 });

        // 기존 워드 10 = 0x0002(비트 1 ON), 워드 11 = 0xA000(비트 13, 15 ON)
        byte[] readResponse = BuildSphReadResponse(connectionId: 1, areaCode: 0x02, startWord: 10, [0x02, 0x00, 0x00, 0xA0]);
        byte[] writeResponse = BuildSphWriteResponse(connectionId: 1, status: 0x00);
        transport.EnqueueResponse(readResponse);
        transport.EnqueueResponse(writeResponse);

        // 20비트 쓰기: 비트 0 = 1, 비트 1 = 0(기존 ON을 OFF로 변경), 비트 19(워드 11 비트 3) = 1
        byte[] bitValues = new byte[20];
        bitValues[0] = 1;
        bitValues[19] = 1;

        var request = new PlcWriteRequest("M1.10.0", PlcDataType.Boolean, bitValues);
        await protocol.WriteAsync(request, default);

        Assert.Equal(2, transport.SentFrames.Count);

        // 1번째 프레임: M1.10부터 2워드 읽기(0x00)
        byte[] sentRead = transport.SentFrames[0];
        Assert.Equal(0x00, sentRead[14]);
        Assert.Equal(10, sentRead[21]);
        Assert.Equal(2, sentRead[24]);

        // 2번째 프레임: M1.10부터 2워드 쓰기(0x01), 워드 10 = 0x0001(01 00), 워드 11 = 0xA008(08 A0, 기존 상위 비트 보존)
        byte[] sentWrite = transport.SentFrames[1];
        Assert.Equal(0x01, sentWrite[14]);
        Assert.Equal(10, sentWrite[21]);
        Assert.Equal(2, sentWrite[24]);
        Assert.Equal(new byte[] { 0x01, 0x00, 0x08, 0xA0 }, sentWrite[26..30]);
    }

    [Fact]
    public async Task ReadAsync_And_WriteAsync_Sph_Int32_ChunksByMultiWordElementAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new FujiElectricSphOptions { ConnectionId = 1, MaxReadWords = 4, MaxWriteWords = 4 };
        var protocol = new FujiElectricSphProtocol(transport, options);

        // Int32 3개(6워드) 읽기 -> 1청크: M1.100 4워드, 2청크: M1.104 2워드
        transport.EnqueueResponse(BuildSphReadResponse(1, 0x02, 100, [1, 0, 2, 0, 3, 0, 4, 0]));
        transport.EnqueueResponse(BuildSphReadResponse(1, 0x02, 104, [5, 0, 6, 0]));

        var readRequest = new PlcReadRequest("M1.100", PlcDataType.Int32, 3);
        byte[] readBytes = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(12, readBytes.Length);
        Assert.Equal(100, transport.SentFrames[0][21]);
        Assert.Equal(4, transport.SentFrames[0][24]);
        Assert.Equal(104, transport.SentFrames[1][21]);
        Assert.Equal(2, transport.SentFrames[1][24]);

        // Int32 3개(6워드) 쓰기 -> 2번째 청크에서 장치 오류(0x40) 발생 시 CompletedCount == 2 보존
        transport.EnqueueResponse(BuildSphWriteResponse(connectionId: 1, status: 0x00));
        transport.EnqueueResponse(BuildSphWriteResponse(connectionId: 1, status: 0x40));

        var writeRequest = new PlcWriteRequest("M1.100", PlcDataType.Int32, readBytes);
        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(
            () => protocol.WriteAsync(writeRequest, default));

        Assert.Equal(2, exception.CompletedCount);
        Assert.Equal(100, transport.SentFrames[2][21]);
        Assert.Equal(4, transport.SentFrames[2][24]);
        Assert.Equal(104, transport.SentFrames[3][21]);
        Assert.Equal(2, transport.SentFrames[3][24]);
    }

    private static byte[] BuildSphReadResponse(byte connectionId, byte areaCode, uint startWord, byte[] wordData)
    {
        ushort payloadLength = (ushort)(6 + wordData.Length);
        ushort wordCount = (ushort)(wordData.Length / 2);
        byte[] response = new byte[20 + payloadLength];
        response[0] = 0xFB;
        response[1] = 0x80;
        response[2] = 0x80;
        response[4] = 0x00;
        response[5] = 0x7B;
        response[6] = connectionId;
        response[14] = 0x00;
        response[17] = 0x01;
        response[18] = (byte)(payloadLength & 0xFF);
        response[19] = (byte)((payloadLength >> 8) & 0xFF);
        response[20] = areaCode;
        response[21] = (byte)(startWord & 0xFF);
        response[22] = (byte)((startWord >> 8) & 0xFF);
        response[23] = (byte)((startWord >> 16) & 0xFF);
        response[24] = (byte)(wordCount & 0xFF);
        response[25] = (byte)((wordCount >> 8) & 0xFF);
        System.Buffer.BlockCopy(wordData, 0, response, 26, wordData.Length);
        return response;
    }

    private static byte[] BuildSphWriteResponse(byte connectionId, byte status)
    {
        byte[] response = new byte[26];
        response[0] = 0xFB;
        response[1] = 0x80;
        response[2] = 0x80;
        response[4] = status;
        response[5] = 0x7B;
        response[6] = connectionId;
        response[14] = 0x01;
        response[17] = 0x01;
        response[18] = 0x06;
        response[19] = 0x00;
        return response;
    }
}
