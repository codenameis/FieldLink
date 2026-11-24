using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Delta;
using FieldLink.Plc.Protocols.GE;
using FieldLink.Plc.Protocols.Inovance;
using FieldLink.Plc.Protocols.Jtekt;
using FieldLink.Plc.Protocols.Keyence;
using FieldLink.Plc.Protocols.Megmeet;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Plc.Protocols.Vigor;
using FieldLink.Plc.Protocols.Xinje;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Shared;

public sealed class ProtocolExecutionGuardTests
{
    [Fact]
    public void ValidateAddressAndDataType_BitAddressWithWordDataType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ProtocolExecutionGuard.ValidateAddressAndDataType(true, PlcDataType.Int16));
    }

    [Fact]
    public void ValidateAddressAndDataType_WordAddressWithBooleanDataType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ProtocolExecutionGuard.ValidateAddressAndDataType(false, PlcDataType.Boolean));
    }

    [Fact]
    public void NormalizeWordReadRequest_ByteCountOdd_RoundsUpWordCountAndTrimsResult()
    {
        var request = new PlcReadRequest("D100", PlcDataType.Byte, 3);
        PlcReadRequest normalized = ProtocolExecutionGuard.NormalizeWordReadRequest(request);

        Assert.Equal(PlcDataType.UInt16, normalized.DataType);
        Assert.Equal(2, normalized.Count);

        byte[] rawFourBytes = new byte[] { 0x41, 0x42, 0x43, 0x00 };
        byte[] trimmed = ProtocolExecutionGuard.TrimReadResult(rawFourBytes, request.ByteCount);

        Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, trimmed);
    }

    [Fact]
    public void NormalizeWordWriteRequest_OddByteCount_ThrowsArgumentException()
    {
        var oddByteRequest = new PlcWriteRequest("D100", PlcDataType.Byte, new byte[] { 0x01, 0x02, 0x03 });
        Assert.Throws<ArgumentException>(() =>
            ProtocolExecutionGuard.NormalizeWordWriteRequest(oddByteRequest, out _));

        var oddStringRequest = new PlcWriteRequest("D100", PlcDataType.String, new byte[] { 0x41, 0x42, 0x43 });
        Assert.Throws<ArgumentException>(() =>
            ProtocolExecutionGuard.NormalizeWordWriteRequest(oddStringRequest, out _));
    }

    [Fact]
    public async Task Inovance_AmSeries_Mx5_3_BitReadAndWrite_UsesHoldingRegisterAndReadModifyWrite()
    {
        Assert.False(InovanceAddress.TryParse("MXabc.1", InovanceSeries.AM, true, out _, out _));
        Assert.False(InovanceAddress.TryParse("SMabc", InovanceSeries.H3u, true, out _, out _));

        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(
            transport,
            new InovanceOptions { Series = InovanceSeries.AM, Station = 1 },
            InovanceTransportEncoding.Rtu);

        // MX5.3 -> HoldingRegister PDU 2, BitIndexInWord 11 (1 << 11 = 0x0800)
        // 1) ReadBoolean("MX5.3"): FC 0x03 response with 0x0800 -> bit 11 is 1
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x03, 0x02, 0x08, 0x00 }));

        // 2) WriteBoolean("MX5.3", true): Read FC 0x03 (current = 0x0001) + WriteSingleRegister FC 0x06 (updated = 0x0801)
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x03, 0x02, 0x00, 0x01 }));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x06, 0x00, 0x02, 0x08, 0x01 }));

        byte[] readResult = await protocol.ReadAsync(new PlcReadRequest("MX5.3", PlcDataType.Boolean, 1), default);
        Assert.Equal(new byte[] { 1 }, readResult);

        await protocol.WriteAsync(new PlcWriteRequest("MX5.3", PlcDataType.Boolean, new byte[] { 1 }), default);

        Assert.Equal(3, transport.SentFrames.Count);
        byte[] readPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal((byte)ModbusFunctionCode.ReadHoldingRegisters, readPdu[0]);

        byte[] rmwReadPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal((byte)ModbusFunctionCode.ReadHoldingRegisters, rmwReadPdu[0]);

        byte[] rmwWritePdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[2], 1);
        Assert.Equal((byte)ModbusFunctionCode.WriteSingleRegister, rmwWritePdu[0]);
        Assert.Equal(new byte[] { 0x06, 0x00, 0x02, 0x08, 0x01 }, rmwWritePdu);
    }

    [Fact]
    public async Task GESrtp_ByteSegment_M1_ReadAndWriteInt16_UsesFourByteWireQuantity()
    {
        var transport = new ScriptedTransport();
        var protocol = new GESrtpProtocol(transport);

        // Init handshake response (56 bytes, byte[0] = 0x01)
        byte[] initResponse = new byte[56];
        initResponse[0] = 0x01;
        transport.EnqueueResponse(initResponse);

        // Piggyback response for ReadInt16("M1", count: 2) -> 4 bytes in header[44..48]
        byte[] readResponse = new byte[56];
        readResponse[0] = 0x03;
        readResponse[2] = 0x01; // seqId = 1
        readResponse[31] = 0xD4; // ShortAckMessageType
        readResponse[44] = 0x34;
        readResponse[45] = 0x12;
        readResponse[46] = 0x78;
        readResponse[47] = 0x56;
        transport.EnqueueResponse(readResponse);

        // Write response for WriteInt16("M1", count: 2) -> 4 bytes
        byte[] writeResponse = new byte[56];
        writeResponse[0] = 0x03;
        writeResponse[2] = 0x02; // seqId = 2
        writeResponse[31] = 0xD4; // ShortAckMessageType
        transport.EnqueueResponse(writeResponse);

        byte[] readBytes = await protocol.ReadAsync(new PlcReadRequest("M1", PlcDataType.Int16, 2), default);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, readBytes);

        // SentFrames[1] is the Read request; offset 46..47 is wireQuantity (LE) -> must be 4
        byte[] sentReadFrame = transport.SentFrames[1];
        ushort readWireQuantity = (ushort)(sentReadFrame[46] | (sentReadFrame[47] << 8));
        Assert.Equal(4, readWireQuantity);

        await protocol.WriteAsync(new PlcWriteRequest("M1", PlcDataType.Int16, new byte[] { 0x34, 0x12, 0x78, 0x56 }), default);
        byte[] sentWriteFrame = transport.SentFrames[2];
        ushort writeWireQuantity = (ushort)(sentWriteFrame[54] | (sentWriteFrame[55] << 8));
        Assert.Equal(4, writeWireQuantity);
    }

    [Fact]
    public async Task Delta_TimerAndCounterBitAccess_UsesCoilFunctionCodes()
    {
        var transport = new ScriptedTransport();
        var protocol = new DeltaProtocol(
            transport,
            new DeltaOptions { Series = DeltaSeries.Dvp, Station = 1 },
            DeltaTransportEncoding.Rtu);

        // ReadBoolean("T5") -> FC 0x01 at PDU 0x0605
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x01, 0x01, 0x01 }));
        // WriteBoolean("C3", true) -> FC 0x0F at PDU 0x0E03
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x0F, 0x0E, 0x03, 0x00, 0x01 }));

        byte[] readBit = await protocol.ReadAsync(new PlcReadRequest("T5", PlcDataType.Boolean, 1), default);
        Assert.Equal(new byte[] { 1 }, readBit);

        await protocol.WriteAsync(new PlcWriteRequest("C3", PlcDataType.Boolean, new byte[] { 1 }), default);

        byte[] sentReadPdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal((byte)ModbusFunctionCode.ReadCoils, sentReadPdu[0]);
        Assert.Equal(0x0605, (sentReadPdu[1] << 8) | sentReadPdu[2]);

        byte[] sentWritePdu = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal((byte)ModbusFunctionCode.WriteMultipleCoils, sentWritePdu[0]);
        Assert.Equal(0x0E03, (sentWritePdu[1] << 8) | sentWritePdu[2]);
    }

    [Fact]
    public async Task Megmeet_X7OffsetAnd32BitCounterBoundary_BehavesPerManual()
    {
        MegmeetAddress x7 = MegmeetAddress.Parse("X7", isBitAccess: true);
        MegmeetAddress x10 = x7.Offset(1);
        Assert.Equal(8, x10.PduAddress);
        Assert.Equal(ModbusArea.DiscreteInput, x10.Area);

        var transport = new ScriptedTransport();
        var protocol = new MegmeetProtocol(transport, new MegmeetOptions { Station = 1 }, MegmeetTransportEncoding.Rtu);

        // 16-bit read on C200 must throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            protocol.ReadAsync(new PlcReadRequest("C200", PlcDataType.Int16, 1), default));

        // C254 Int32 count 4 -> splits at C256 into [C254..C255 (PDU 9808, 4 words)] and [C256..C257 (PDU 10000, 4 words)]
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x03, 8, 1, 2, 3, 4, 5, 6, 7, 8 }));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x03, 8, 9, 10, 11, 12, 13, 14, 15, 16 }));

        byte[] readResult = await protocol.ReadAsync(new PlcReadRequest("C254", PlcDataType.Int32, 4), default);
        Assert.Equal(16, readResult.Length);
        Assert.Equal(2, transport.SentFrames.Count);

        byte[] pdu1 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[0], 1);
        Assert.Equal(9808, (pdu1[1] << 8) | pdu1[2]);
        Assert.Equal(4, (pdu1[3] << 8) | pdu1[4]);

        byte[] pdu2 = ModbusResponseParser.UnwrapRtuAdu(transport.SentFrames[1], 1);
        Assert.Equal(10000, (pdu2[1] << 8) | pdu2[2]);
        Assert.Equal(4, (pdu2[3] << 8) | pdu2[4]);
    }

    [Fact]
    public async Task Vigor_Keyence_Toyopuc_Xinje_RegressionChecks()
    {
        // 1) Vigor C200 16-bit rejection
        var vigorTransport = new ScriptedTransport();
        var vigorProtocol = new VigorProtocol(vigorTransport);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            vigorProtocol.ReadAsync(new PlcReadRequest("C200", PlcDataType.Int16, 1), default));

        // 2) Keyence MR16 / R099 invalid bit offset rejection
        Assert.False(KeyenceHostLinkAddress.TryParse("MR16", out _, out _));
        Assert.False(KeyenceHostLinkAddress.TryParse("R099", out _, out _));
        Assert.True(KeyenceHostLinkAddress.TryParse("MR115", out KeyenceHostLinkAddress? mr115, out _));
        Assert.Equal(1, mr115!.Channel);
        Assert.Equal(15, mr115.Bit);

        // 3) Keyence DisconnectAsync consumes CF\r\n and composite T0 / 32-bit TC0 parsing
        var keyenceTransport = new ScriptedTransport();
        var keyenceProtocol = new KeyenceHostLinkProtocol(keyenceTransport);
        keyenceTransport.EnqueueResponse(Encoding.ASCII.GetBytes("CF\r\n"));
        await keyenceProtocol.DisconnectAsync(default);
        Assert.Single(keyenceTransport.SentFrames);
        Assert.Equal("CQ\r", Encoding.ASCII.GetString(keyenceTransport.SentFrames[0]));

        keyenceTransport.EnqueueResponse(Encoding.ASCII.GetBytes("1,00123,00500\r\n"));
        byte[] timerCurrentBytes = await keyenceProtocol.ReadAsync(new PlcReadRequest("T0", PlcDataType.Int32, 1), default);
        Assert.Equal(BitConverter.GetBytes(123), timerCurrentBytes);

        // 4) Toyopuc EB7FFF.Offset(1) -> Prg = 0x0A, WireAddress = 0x0000
        ToyopucAddress eb7fff = ToyopucAddress.Parse("EB7FFF");
        Assert.Equal((byte)0x09, eb7fff.Prg);
        Assert.Equal((ushort)0x7FFF, eb7fff.WireAddress);

        ToyopucAddress eb8000 = eb7fff.Offset(1);
        Assert.Equal((byte)0x0A, eb8000.Prg);
        Assert.Equal((ushort)0x0000, eb8000.WireAddress);

        // 5) Xinje internal vs extended max words clamping
        Assert.False(XinjeAddress.Parse("D100").IsExtended);
        Assert.True(XinjeAddress.Parse("D20480").IsExtended);
        Assert.True(XinjeAddress.Parse("HD0").IsExtended);
    }

    [Fact]
    public async Task ModbusRtuProtocol_ReadStringAndWriteString_ViaPlcClient_Succeeds()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x03, 0x04, 0x41, 0x42, 0x43, 0x44 }));
        transport.EnqueueResponse(ModbusRequestBuilder.BuildRtuAdu(1, new byte[] { 0x10, 0x00, 0x00, 0x00, 0x02 }));

        var protocol = new ModbusRtuProtocol(transport, new ModbusRtuOptions(slaveAddress: 1));
        using var client = new PlcClient(transport, protocol);
        await client.OpenAsync();

        PlcResult<string> readResult = await client.ReadStringAsync("40001", 4);
        Assert.True(readResult.IsSuccess);
        Assert.Equal("ABCD", readResult.Value);

        PlcResult writeResult = await client.WriteStringAsync("40001", "ABCD", 4);
        Assert.True(writeResult.IsSuccess);
    }

    [Fact]
    public async Task MCBinaryProtocol_ReadBytesAndWriteBytes_Succeeds()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(Convert.FromHexString("D00000FFFF03000600000011223344"));
        transport.EnqueueResponse(Convert.FromHexString("D00000FFFF030002000000"));

        var protocol = new MCBinaryProtocol(transport, new MCOptions());

        byte[] readBytes = await protocol.ReadAsync(new PlcReadRequest("D100", PlcDataType.Byte, 3), default);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, readBytes);

        await protocol.WriteAsync(new PlcWriteRequest("D100", PlcDataType.Byte, new byte[] { 0x11, 0x22, 0x33, 0x44 }), default);
    }

    [Fact]
    public async Task MCBinaryProtocol_ChunkedWritePartialFailure_ReportsCompletedCount()
    {
        var transport = new ScriptedTransport();
        transport.EnqueueResponse(Convert.FromHexString("D00000FFFF030002000000"));
        transport.EnqueueResponse(Convert.FromHexString("D00000FFFF0300020051C0"));

        var options = new MCOptions(maxWordPoints: 2);
        var protocol = new MCBinaryProtocol(transport, options);

        byte[] payload = new byte[8]; // 2 Int32 elements = 4 words (2 chunks of 2 words)
        PlcDeviceException exception = await Assert.ThrowsAsync<PlcDeviceException>(() =>
            protocol.WriteAsync(new PlcWriteRequest("D100", PlcDataType.Int32, payload), default));

        Assert.Equal(1, exception.CompletedCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void NormalizeWordRequests_InvalidBytesPerDeviceUnit_ThrowsArgumentOutOfRangeException(int bytesPerDeviceUnit)
    {
        var readRequest = new PlcReadRequest("D100", PlcDataType.Byte, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProtocolExecutionGuard.NormalizeWordReadRequest(readRequest, bytesPerDeviceUnit));

        var writeRequest = new PlcWriteRequest("D100", PlcDataType.Byte, new byte[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProtocolExecutionGuard.NormalizeWordWriteRequest(writeRequest, out _, bytesPerDeviceUnit));
    }

    [Fact]
    public void NormalizeWordReadRequest_MaxAlignedByteCount_DoesNotOverflow()
    {
        var maxWordAlignedRequest = new PlcReadRequest("D0", PlcDataType.Byte, int.MaxValue - 1);
        PlcReadRequest normalizedWord = ProtocolExecutionGuard.NormalizeWordReadRequest(maxWordAlignedRequest);

        Assert.Equal(PlcDataType.UInt16, normalizedWord.DataType);
        Assert.Equal(int.MaxValue / 2, normalizedWord.Count);

        var maxDWordAlignedRequest = new PlcReadRequest("D0", PlcDataType.Byte, int.MaxValue - 3);
        PlcReadRequest normalizedDWord = ProtocolExecutionGuard.NormalizeWordReadRequest(maxDWordAlignedRequest, 4);

        Assert.Equal(PlcDataType.UInt32, normalizedDWord.DataType);
        Assert.Equal(int.MaxValue / 4, normalizedDWord.Count);
    }

    [Fact]
    public void NormalizeWordReadRequest_MaxByteCount_DoesNotWrapNegative()
    {
        var maxRequest = new PlcReadRequest("D0", PlcDataType.Byte, int.MaxValue);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ProtocolExecutionGuard.NormalizeWordReadRequest(maxRequest));

        Assert.Equal((int.MaxValue / 2) + 1, exception.ActualValue);
    }
}
