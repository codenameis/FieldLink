using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Fatek;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Fatek;

public class FatekProtocolTests
{
    private static byte[] MakeResponse(byte station, string command, char status, string data = "")
    {
        string headerAndData = $"{station:X2}{command}{status}{data}";
        int sum = 0x02;
        foreach (char character in headerAndData)
        {
            sum += (byte)character;
        }

        string lrc = (sum & 0xFF).ToString("X2");
        string frame = $"\x02{headerAndData}{lrc}\x03";
        return Encoding.ASCII.GetBytes(frame);
    }

    [Fact]
    public async Task ReadAsync_DiscreteBits_Command44_BuildsRequestAndExtractsBits()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "44", '0', "1010");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 4);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        Assert.Equal(1, result[0]);
        Assert.Equal(0, result[1]);
        Assert.Equal(1, result[2]);
        Assert.Equal(0, result[3]);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014404X0000", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task ReadAsync_WordRegisters_Command46_BuildsRequestAndDecodesLittleEndian()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "46", '0', "1234ABCD");
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("R100", PlcDataType.Int16, 2);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(4, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);
        Assert.Equal(0xCD, result[2]);
        Assert.Equal(0xAB, result[3]);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014602R00100", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task WriteAsync_Bits_Command45_BuildsCorrectRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "45", '0');
        transport.EnqueueResponse(response);

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("Y0", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014503Y0000101", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task WriteAsync_Words_Command47_BuildsCorrectRequest()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "47", '0');
        transport.EnqueueResponse(response);

        byte[] wordData = [0x34, 0x12];
        var request = new PlcWriteRequest("D0", PlcDataType.Int16, wordData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        string sentAscii = Encoding.ASCII.GetString(transport.SentFrames[0]);
        Assert.StartsWith("\x02" + "014701D000001234", sentAscii);
        Assert.EndsWith("\x03", sentAscii);
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        byte[] response = MakeResponse(1, "44", '2');
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("X0", PlcDataType.Boolean, 1);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal((int)'2', exception.DeviceErrorCode);
        Assert.Contains("Illegal value", exception.Message);
    }

    [Fact]
    public async Task ReadAndWriteAsync_Int32_ChunksByTwoWordsPerElementAndPreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        var options = new FatekOptions { Station = 1, MaxWordsPerRequest = 2 };
        var protocol = new FatekProtocol(transport, options);

        // D100에서 Int32 2개 읽기 (MaxWordsPerRequest=2이므로 청크당 Int32 1개 = 2워드씩 분할: D00100, D00102)
        transport.EnqueueResponse(MakeResponse(1, "46", '0', "12345678"));
        transport.EnqueueResponse(MakeResponse(1, "46", '0', "9ABCDEF0"));

        var readRequest = new PlcReadRequest("D100", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE }, readResult);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.StartsWith("\x02" + "014602D00100", Encoding.ASCII.GetString(transport.SentFrames[0]));
        Assert.StartsWith("\x02" + "014602D00102", Encoding.ASCII.GetString(transport.SentFrames[1]));

        // D100에 Int32 2개 분할 쓰기 중 두 번째 청크에서 장치 오류('A') 발생 시 CompletedCount = 1 보존
        transport.EnqueueResponse(MakeResponse(1, "47", '0'));
        transport.EnqueueResponse(MakeResponse(1, "47", 'A'));

        var writeRequest = new PlcWriteRequest("D100", PlcDataType.Int32, readResult);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(writeRequest, default));

        Assert.Equal((int)'A', exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
        Assert.Equal(4, transport.SentFrames.Count);
        Assert.StartsWith("\x02" + "014702D0010012345678", Encoding.ASCII.GetString(transport.SentFrames[2]));
        Assert.StartsWith("\x02" + "014702D001029ABCDEF0", Encoding.ASCII.GetString(transport.SentFrames[3]));
    }

    [Fact]
    public async Task ReadAndWriteAsync_Drc200_32BitCounterRegister_Uses8HexCharsPerElementAndRejects16Bit()
    {
        var transport = new ScriptedTransport();
        var protocol = new FatekProtocol(transport, new FatekOptions { Station = 1 });

        // DRC200에 16비트 접근 거부
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("DRC200", PlcDataType.Int16, 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("DRC200", PlcDataType.Int16, new byte[2]), default));

        // 출처: docs/plc-packet-manual/fatek.md §1.2, §1.5, §2.5 "DRC200 (32비트 카운터 레지스터, 원소당 8 HEX 문자)"
        transport.EnqueueResponse(MakeResponse(1, "46", '0', "123456789ABCDEF0"));

        byte[] readResult = await protocol.ReadAsync(new PlcReadRequest("DRC200", PlcDataType.Int32, 2), default);
        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12, 0xF0, 0xDE, 0xBC, 0x9A }, readResult);
        Assert.Single(transport.SentFrames);
        Assert.StartsWith("\x02" + "014602DRC0200", Encoding.ASCII.GetString(transport.SentFrames[0]));

        transport.EnqueueResponse(MakeResponse(1, "47", '0'));
        await protocol.WriteAsync(new PlcWriteRequest("DRC200", PlcDataType.Int32, new byte[] { 0x78, 0x56, 0x34, 0x12 }), default);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.StartsWith("\x02" + "014701DRC020012345678", Encoding.ASCII.GetString(transport.SentFrames[1]));
    }
}
