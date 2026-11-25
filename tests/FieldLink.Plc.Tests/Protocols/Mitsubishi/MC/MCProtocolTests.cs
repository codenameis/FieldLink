using System;
using System.Net;
using System.Net.Sockets;
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

    [Fact]
    public void Factory_TcpAndUdpMethods_CreateLittleEndianClientsAndValidatePorts()
    {
        using var binaryTcpDefault = PlcClientFactory.CreateMitsubishiMCBinaryTcp("127.0.0.1");
        using var binaryTcpExplicit = PlcClientFactory.CreateMitsubishiMCBinaryTcp("127.0.0.1", port: 5002);
        using var binaryUdpDefault = PlcClientFactory.CreateMitsubishiMCBinaryUdp("127.0.0.1");
        using var binaryUdpExplicit = PlcClientFactory.CreateMitsubishiMCBinaryUdp("127.0.0.1", port: 5002, localPort: 15002);
        using var asciiTcpDefault = PlcClientFactory.CreateMitsubishiMCAsciiTcp("127.0.0.1");
        using var asciiTcpExplicit = PlcClientFactory.CreateMitsubishiMCAsciiTcp("127.0.0.1", port: 5003);
        using var asciiUdpDefault = PlcClientFactory.CreateMitsubishiMCAsciiUdp("127.0.0.1");
        using var asciiUdpExplicit = PlcClientFactory.CreateMitsubishiMCAsciiUdp("127.0.0.1", port: 5003, localPort: 15003);

        Assert.Equal(ByteOrder.LittleEndian, binaryTcpDefault.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, binaryTcpExplicit.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, binaryUdpDefault.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, binaryUdpExplicit.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, asciiTcpDefault.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, asciiTcpExplicit.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, asciiUdpDefault.ByteOrder);
        Assert.Equal(ByteOrder.LittleEndian, asciiUdpExplicit.ByteOrder);

        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCBinaryTcp("127.0.0.1", port: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCBinaryTcp("127.0.0.1", port: 65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCBinaryUdp("127.0.0.1", port: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCBinaryUdp("127.0.0.1", port: 5000, localPort: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCAsciiTcp("127.0.0.1", port: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlcClientFactory.CreateMitsubishiMCAsciiUdp("127.0.0.1", port: 5000, localPort: 65536));
    }

    [Fact]
    public async Task Factory_CreateMitsubishiMCBinaryUdp_LoopbackReadAndContiguousWrite_Succeeds()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        byte[]? capturedReadRequest = null;
        byte[]? capturedWriteRequest = null;

        Task serverTask = Task.Run(async () =>
        {
            UdpReceiveResult firstDatagram = await server.ReceiveAsync().ConfigureAwait(false);
            capturedReadRequest = firstDatagram.Buffer;
            byte[] readResponse = FromHex("D0 00 00 FF FF 03 00 04 00 00 00 34 12");
            await server.SendAsync(readResponse, readResponse.Length, firstDatagram.RemoteEndPoint).ConfigureAwait(false);

            UdpReceiveResult secondDatagram = await server.ReceiveAsync().ConfigureAwait(false);
            capturedWriteRequest = secondDatagram.Buffer;
            byte[] writeResponse = FromHex("D0 00 00 FF FF 03 00 02 00 00 00");
            await server.SendAsync(writeResponse, writeResponse.Length, secondDatagram.RemoteEndPoint).ConfigureAwait(false);
        });

        using var client = PlcClientFactory.CreateMitsubishiMCBinaryUdp("127.0.0.1", port: serverPort);

        PlcResult openResult = await client.OpenAsync();
        Assert.True(openResult.IsSuccess);

        PlcResult<short> readResult = await client.ReadInt16Async("D100");
        Assert.True(readResult.IsSuccess);
        Assert.Equal((short)0x1234, readResult.Value);

        PlcResult writeResult = await client.WriteInt16Async("D100", new short[] { 0x1234, 0x5678 });
        Assert.True(writeResult.IsSuccess);

        await serverTask;
        PlcResult closeResult = await client.CloseAsync();
        Assert.True(closeResult.IsSuccess);

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0C 00 10 00 01 04 00 00 64 00 00 A8 01 00"), capturedReadRequest);
        Assert.Equal(FromHex("50 00 00 FF FF 03 00 10 00 10 00 01 14 00 00 64 00 00 A8 02 00 34 12 78 56"), capturedWriteRequest);
    }

    [Fact]
    public async Task PreFlightAddressRangeOverflow_ThrowsBeforeSendingAnyFrame()
    {
        var binaryTransport = new ScriptedTransport();
        var binaryProtocol = new MCBinaryProtocol(binaryTransport, new MCOptions(MCFrameType.Frame3E, MCSeries.QL));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            binaryProtocol.ReadAsync(new PlcReadRequest("D16777215", PlcDataType.Int16, 2), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            binaryProtocol.WriteAsync(new PlcWriteRequest("D16777215", PlcDataType.Int16, new byte[4]), CancellationToken.None));
        Assert.Empty(binaryTransport.SentFrames);

        var asciiTransport = new ScriptedTransport();
        var asciiProtocol = new MCAsciiProtocol(asciiTransport, new MCOptions(MCFrameType.Frame3E, MCSeries.QL));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            asciiProtocol.ReadAsync(new PlcReadRequest("D999999", PlcDataType.Int16, 2), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            asciiProtocol.WriteAsync(new PlcWriteRequest("D999999", PlcDataType.Int16, new byte[4]), CancellationToken.None));
        Assert.Empty(asciiTransport.SentFrames);
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
