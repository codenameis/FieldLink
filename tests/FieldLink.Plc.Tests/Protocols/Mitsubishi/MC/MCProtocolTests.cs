using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Mitsubishi.MC;

public sealed class MCProtocolTests
{
    [Fact]
    public async Task Binary_3E_ReadWord_SendsVectorM1AndReturnsData()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("D0 00 00 FF FF 03 00 04 00 00 00 34 12"));
        var protocol = new MCBinaryProtocol(transport, new MCOptions(MCFrameType.Frame3E, MCSeries.QL));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0C 00 10 00 01 04 00 00 64 00 00 A8 01 00"), transport.SentFrames[0]);
    }

    [Fact]
    public async Task Binary_4E_LateResponse_IsDiscardedBySerialNumber()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("D4 00 99 00 00 00 00 FF FF 03 00 04 00 00 00 EE EE"));
        transport.EnqueueResponse(FromHex("D4 00 01 00 00 00 00 FF FF 03 00 04 00 00 00 34 12"));
        var protocol = new MCBinaryProtocol(transport, new MCOptions(MCFrameType.Frame4E, MCSeries.QL));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public async Task Binary_ReadBits_ReturnsOneBytePerBit()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("D0 00 00 FF FF 03 00 04 00 00 00 10 10"));
        var protocol = new MCBinaryProtocol(transport);

        byte[] bits = await protocol.ReadAsync(new PlcReadRequest("M10", PlcDataType.Boolean, 3), CancellationToken.None);

        Assert.Equal(new byte[] { 1, 0, 1 }, bits);
    }

    [Fact]
    public async Task Binary_ChunkedRead_AdvancesDeviceNumberAndCombinesData()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 1, 0, 2, 0 }));
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 3, 0, 4, 0 }));
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 5, 0 }));
        var protocol = new MCBinaryProtocol(transport, new MCOptions(maxWordPoints: 2));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D0", PlcDataType.Int16, 5), CancellationToken.None);

        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0 }, data);
        Assert.Equal(3, transport.SentFrames.Count);
        Assert.Equal(new byte[] { 0, 2, 4 }, new[] { transport.SentFrames[0][15], transport.SentFrames[1][15], transport.SentFrames[2][15] });
    }

    [Fact]
    public async Task Binary_Int32_IsNotSplitAcrossChunks()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 1, 0, 0, 0 }));
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 2, 0, 0, 0 }));
        transport.EnqueueResponse(BinaryResponse3E(new byte[] { 3, 0, 0, 0 }));
        var protocol = new MCBinaryProtocol(transport, new MCOptions(maxWordPoints: 3));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D0", PlcDataType.Int32, 3), CancellationToken.None);

        Assert.Equal(12, data.Length);
        Assert.Equal(3, transport.SentFrames.Count);
        Assert.Equal(2, transport.SentFrames[0][19]);
    }

    [Fact]
    public async Task Binary_WriteFailsOnSecondChunk_ReportsDeviceCodeAndCompletedCount()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("D0 00 00 FF FF 03 00 02 00 00 00"));
        transport.EnqueueResponse(FromHex("D0 00 00 FF FF 03 00 02 00 56 C0"));
        var protocol = new MCBinaryProtocol(transport, new MCOptions(maxWordPoints: 2));
        var request = new PlcWriteRequest("D0", PlcDataType.Int16, new byte[8]);

        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, CancellationToken.None));

        Assert.Equal(0xC056, exception.DeviceErrorCode);
        Assert.Equal(2, exception.CompletedCount);
    }

    [Fact]
    public async Task Binary_BooleanOnWordDevice_ThrowsArgumentException()
    {
        var protocol = new MCBinaryProtocol(new ScriptedTransport());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("D0", PlcDataType.Boolean, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Binary_WordTypeOnBitDevice_ThrowsArgumentException()
    {
        var protocol = new MCBinaryProtocol(new ScriptedTransport());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("M0", PlcDataType.Int16, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Ascii_3E_ReadWord_SendsVectorM4AndReturnsData()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("D00000FF03FF00000800001234"));
        var protocol = new MCAsciiProtocol(transport);

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
        Assert.Equal("500000FF03FF000018001004010000D*0001000001", Encoding.ASCII.GetString(transport.SentFrames[0]));
    }

    [Fact]
    public async Task Ascii_4E_Iqr_ReadWord_UsesSerialNumberOne()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("D4000001000000FF03FF00000800001234"));
        var protocol = new MCAsciiProtocol(transport, new MCOptions(MCFrameType.Frame4E, MCSeries.Iqr));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
        Assert.StartsWith("540000010000", Encoding.ASCII.GetString(transport.SentFrames[0]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ascii_WriteWord_Succeeds()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(Encoding.ASCII.GetBytes("D00000FF03FF000004" + "0000"));
        var protocol = new MCAsciiProtocol(transport);

        await protocol.WriteAsync(new PlcWriteRequest("D100", PlcDataType.Int16, new byte[] { 0x34, 0x12 }), CancellationToken.None);

        Assert.EndsWith("1234", Encoding.ASCII.GetString(transport.SentFrames[0]), StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_CreatesLittleEndianClients()
    {
        using var binaryClient = PlcClientFactory.CreateMitsubishiMCBinary(new ScriptedTransport());
        using var asciiClient = PlcClientFactory.CreateMitsubishiMCAscii(new ScriptedTransport());

        Assert.Equal(ByteOrder.LittleEndian, binaryClient.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, asciiClient.ByteOrder);
    }

    private static byte[] BinaryResponse3E(byte[] data)
    {
        var frame = new byte[11 + data.Length];
        byte[] header = FromHex("D0 00 00 FF FF 03 00");
        Array.Copy(header, frame, header.Length);
        int bodyLength = 2 + data.Length;
        frame[7] = (byte)bodyLength;
        frame[8] = (byte)(bodyLength >> 8);
        Array.Copy(data, 0, frame, 11, data.Length);
        return frame;
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
