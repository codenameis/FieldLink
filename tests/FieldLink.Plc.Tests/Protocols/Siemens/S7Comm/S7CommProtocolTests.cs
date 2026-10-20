using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Siemens.S7Comm;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Siemens.S7Comm;

public sealed class S7CommProtocolTests
{
    private const string ConnectionConfirm = "03 00 00 16 11 D0 00 01 00 01 00 C0 01 0A C1 02 01 00 C2 02 01 01";

    [Fact]
    public async Task Read_PerformsHandshakeThenReadsWord()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 480);
        transport.EnqueueResponse(Wrap("32 03 00 00 00 02 00 02 00 06 00 00 04 01 FF 04 00 10 12 34"));
        var protocol = new S7CommProtocol(transport, new S7CommOptions(rack: 0, slot: 1));

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("DB1.DBW10", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x12, 0x34 }, data);
        Assert.Equal(3, transport.SentFrames.Count);
        Assert.Equal(FromHex("03 00 00 16 11 E0 00 00 00 01 00 C1 02 01 00 C2 02 01 01 C0 01 0A"), transport.SentFrames[0]);
        Assert.Equal(FromHex("03 00 00 19 02 F0 80 32 01 00 00 00 01 00 08 00 00 F0 00 00 01 00 01 01 E0"), transport.SentFrames[1]);
        Assert.Equal(
            FromHex("03 00 00 1F 02 F0 80 32 01 00 00 00 02 00 0E 00 00 04 01 12 0A 10 02 00 02 00 01 84 00 00 50"),
            transport.SentFrames[2]);
    }

    [Fact]
    public async Task Handshake_HappensOnlyOncePerConnection()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 480);
        transport.EnqueueResponse(ReadResponse(2, new byte[] { 1, 2 }));
        transport.EnqueueResponse(ReadResponse(3, new byte[] { 3, 4 }));
        var protocol = new S7CommProtocol(transport);
        var request = new PlcReadRequest("MB0", PlcDataType.Byte, 2);

        await protocol.ReadAsync(request, CancellationToken.None);
        await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(4, transport.SentFrames.Count);
    }

    [Fact]
    public async Task Read_LateResponse_IsDiscardedByPduReference()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 480);
        transport.EnqueueResponse(ReadResponse(99, new byte[] { 0xEE, 0xEE }));
        transport.EnqueueResponse(ReadResponse(2, new byte[] { 0x12, 0x34 }));
        var protocol = new S7CommProtocol(transport);

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("DB1.DBW10", PlcDataType.Int16, 1), CancellationToken.None);

        Assert.Equal(new byte[] { 0x12, 0x34 }, data);
    }

    [Fact]
    public async Task Read_LongerThanPdu_IsSplitAndAddressAdvances()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 30);
        transport.EnqueueResponse(ReadResponse(2, Sequence(1, 12)));
        transport.EnqueueResponse(ReadResponse(3, Sequence(13, 12)));
        transport.EnqueueResponse(ReadResponse(4, Sequence(25, 6)));
        var protocol = new S7CommProtocol(transport);

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("MB0", PlcDataType.Byte, 30), CancellationToken.None);

        Assert.Equal(Sequence(1, 30), data);
        Assert.Equal(5, transport.SentFrames.Count);
        Assert.Equal(new byte[] { 0x00, 0x60, 0xC0 }, new[] { transport.SentFrames[2][30], transport.SentFrames[3][30], transport.SentFrames[4][30] });
    }

    [Fact]
    public async Task Read_Bit_ReturnsZeroOrOneAndSplitsPerBit()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 480);
        transport.EnqueueResponse(BitResponse(2, 1));
        transport.EnqueueResponse(BitResponse(3, 0));
        transport.EnqueueResponse(BitResponse(4, 1));
        var protocol = new S7CommProtocol(transport);

        byte[] data = await protocol.ReadAsync(new PlcReadRequest("M10.6", PlcDataType.Boolean, 3), CancellationToken.None);

        Assert.Equal(new byte[] { 1, 0, 1 }, data);
        Assert.Equal(5, transport.SentFrames.Count);
        Assert.Equal(0x56, transport.SentFrames[2][30]);
        Assert.Equal(0x57, transport.SentFrames[3][30]);
        Assert.Equal(0x58, transport.SentFrames[4][30]);
    }

    [Fact]
    public async Task Write_SendsDataAndAcceptsAcknowledgement()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 480);
        transport.EnqueueResponse(Wrap("32 03 00 00 00 02 00 02 00 01 00 00 05 01 FF"));
        var protocol = new S7CommProtocol(transport);

        await protocol.WriteAsync(new PlcWriteRequest("DB1.DBW10", PlcDataType.Int16, new byte[] { 0x12, 0x34 }), CancellationToken.None);

        byte[] sent = transport.SentFrames[2];
        Assert.Equal(0x05, sent[17]);
        Assert.Equal(new byte[] { 0x00, 0x04, 0x00, 0x10, 0x12, 0x34 }, sent[^6..]);
    }

    [Fact]
    public async Task Write_DeviceRejectsSecondChunk_KeepsCodeAndCountsWholeElements()
    {
        ScriptedTransport transport = CreateHandshakenTransport(pduLength: 30);
        transport.EnqueueResponse(Wrap("32 03 00 00 00 02 00 02 00 01 00 00 05 01 FF"));
        transport.EnqueueResponse(Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 05"));
        var protocol = new S7CommProtocol(transport);
        var request = new PlcWriteRequest("DB1.DBW0", PlcDataType.Int16, new byte[4]);

        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, CancellationToken.None));

        Assert.Equal(0x05, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    [Fact]
    public async Task Handshake_Failure_IsRetriedOnNextCall()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex("03 00 00 16 11 E0 00 01 00 01 00 C0 01 0A C1 02 01 00 C2 02 01 01"));
        var protocol = new S7CommProtocol(transport);
        var request = new PlcReadRequest("MB0", PlcDataType.Byte, 2);

        var exception = await Assert.ThrowsAsync<PlcException>(() => protocol.ReadAsync(request, CancellationToken.None));
        Assert.Equal(PlcErrorKind.ConnectionFailed, exception.Kind);

        transport.EnqueueResponse(FromHex(ConnectionConfirm));
        transport.EnqueueResponse(SetupResponse(480));
        transport.EnqueueResponse(ReadResponse(2, new byte[] { 7, 8 }));
        byte[] data = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 7, 8 }, data);
        Assert.Equal(4, transport.SentFrames.Count);
        Assert.Equal(transport.SentFrames[0], transport.SentFrames[1]);
    }

    [Fact]
    public async Task DataTypeMismatch_ThrowsArgumentException()
    {
        var protocol = new S7CommProtocol(new ScriptedTransport());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("MB0", PlcDataType.Boolean, 1), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("M0.0", PlcDataType.Int16, 1), CancellationToken.None));
    }

    [Fact]
    public async Task InvalidAddress_ThrowsFormatException()
    {
        var protocol = new S7CommProtocol(new ScriptedTransport());

        await Assert.ThrowsAsync<FormatException>(() =>
            protocol.ReadAsync(new PlcReadRequest("T1", PlcDataType.Int16, 1), CancellationToken.None));
    }

    [Fact]
    public void Factory_CreatesBigEndianClient()
    {
        using PlcClient client = PlcClientFactory.CreateSiemensS7Comm(new ScriptedTransport());

        Assert.Equal(ByteOrder.BigEndian, client.ByteOrder);
    }

    private static ScriptedTransport CreateHandshakenTransport(int pduLength)
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(FromHex(ConnectionConfirm));
        transport.EnqueueResponse(SetupResponse(pduLength));
        return transport;
    }

    private static byte[] SetupResponse(int pduLength)
    {
        return Wrap($"32 03 00 00 00 01 00 08 00 00 00 00 F0 00 00 01 00 01 {pduLength >> 8:X2} {pduLength & 0xFF:X2}");
    }

    private static byte[] ReadResponse(ushort pduReference, byte[] values)
    {
        int dataLength = 4 + values.Length;
        byte[] s7 = new byte[12 + 2 + dataLength];
        FromHex("32 03 00 00").CopyTo(s7, 0);
        s7[4] = (byte)(pduReference >> 8);
        s7[5] = (byte)pduReference;
        s7[7] = 0x02;
        s7[8] = (byte)(dataLength >> 8);
        s7[9] = (byte)dataLength;
        FromHex("04 01 FF 04").CopyTo(s7, 12);
        int bitLength = values.Length * 8;
        s7[16] = (byte)(bitLength >> 8);
        s7[17] = (byte)bitLength;
        values.CopyTo(s7, 18);
        return WrapBytes(s7);
    }

    private static byte[] BitResponse(ushort pduReference, byte value)
    {
        string reference = $"{pduReference >> 8:X2} {pduReference & 0xFF:X2}";
        return Wrap($"32 03 00 00 {reference} 00 02 00 05 00 00 04 01 FF 03 00 01 {value:X2}");
    }

    private static byte[] Sequence(byte first, int count)
    {
        byte[] values = new byte[count];
        for (int index = 0; index < count; index++)
        {
            values[index] = (byte)(first + index);
        }

        return values;
    }

    private static byte[] Wrap(string s7Hex) => WrapBytes(FromHex(s7Hex));

    private static byte[] WrapBytes(byte[] s7)
    {
        int totalLength = 7 + s7.Length;
        byte[] frame = new byte[totalLength];
        FromHex("03 00 00 00 02 F0 80").CopyTo(frame, 0);
        frame[2] = (byte)(totalLength >> 8);
        frame[3] = (byte)totalLength;
        s7.CopyTo(frame, 7);
        return frame;
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
