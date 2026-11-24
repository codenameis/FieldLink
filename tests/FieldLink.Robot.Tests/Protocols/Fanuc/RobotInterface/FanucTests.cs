using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Fanuc.RobotInterface;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Fanuc.RobotInterface;

public sealed class FanucTests
{
    [Fact]
    public void Address_Parse_SpecVectorsAndInvalidCases()
    {
        // 출처: docs/specs/robot/04-fanuc-interface.md §9.2 Vector 3
        FanucAddress sdoLow = FanucAddress.Parse("SDO100", isBit: true);
        Assert.Equal(FanucSelector.InputRelay, sdoLow.Selector);
        Assert.Equal(100, sdoLow.Address);

        FanucAddress sdoHigh = FanucAddress.Parse("SDO11005", isBit: true);
        Assert.Equal(FanucSelector.InternalRelay, sdoHigh.Selector);
        Assert.Equal(5, sdoHigh.Address);

        FanucAddress rdi = FanucAddress.Parse("RDI10", isBit: true);
        Assert.Equal(FanucSelector.OutputRelay, rdi.Selector);
        Assert.Equal(5010, rdi.Address);

        FanucAddress goHigh = FanucAddress.Parse("GO10005", isBit: false);
        Assert.Equal(FanucSelector.AnalogInput, goHigh.Selector);
        Assert.Equal(4005, goHigh.Address);

        FanucAddress r3 = FanucAddress.Parse("R3", isBit: false);
        Assert.Equal(FanucSelector.Data, r3.Selector);
        Assert.Equal(3455, r3.Address);

        FanucAddress sr2 = FanucAddress.Parse("SR2", isBit: false);
        Assert.Equal(FanucSelector.Data, sr2.Selector);
        Assert.Equal(5931, sr2.Address);

        Assert.Throws<FormatException>(() => FanucAddress.Parse("R11", isBit: false));
        Assert.Throws<FormatException>(() => FanucAddress.Parse("SR7", isBit: false));
        Assert.Throws<FormatException>(() => FanucAddress.Parse("D1", isBit: true));
        Assert.Throws<FormatException>(() => FanucAddress.Parse("SDO1", isBit: false));
        Assert.Throws<FormatException>(() => FanucAddress.Parse("UNKNOWN9", isBit: false));
    }

    [Fact]
    public void RequestBuilder_SpecVectors_MatchExactBytes()
    {
        // 출처: docs/specs/robot/04-fanuc-interface.md §9.2 Vector 1 (Connect ClientId=1024)
        byte[] connect = FanucRequestBuilder.BuildConnectRequest(1024);
        Assert.Equal(56, connect.Length);
        Assert.Equal(FakeTransport.ParseHex("00 00 04 00 00 00 00 00"), connect[..8]);
        Assert.All(connect[8..], b => Assert.Equal(0, b));

        // 출처: docs/specs/robot/04-fanuc-interface.md §9.2 Vector 2 Read("D1", length=2)
        byte[] expectedReadD1 = FakeTransport.ParseHex(
            "02 00 06 00 00 00 00 00 00 01 00 00 00 00 00 00 " +
            "00 01 00 00 00 00 00 00 00 00 00 00 00 00 06 C0 " +
            "00 00 00 00 10 0E 00 00 01 01 04 08 00 00 02 00 " +
            "00 00 00 00 00 00 00 00");

        byte[] actualReadD1 = FanucRequestBuilder.BuildReadRequest(FanucSelector.Data, 1, 2);
        Assert.Equal(expectedReadD1, actualReadD1);
    }

    [Fact]
    public void RequestBuilder_InlineVsExtendedWrite_FormatsCorrectHeadersAndLength()
    {
        byte[] inlineWrite = FanucRequestBuilder.BuildWriteRequest(FanucSelector.Data, 10, new byte[] { 0x01, 0x02, 0x03, 0x04 }, 2);
        Assert.Equal(56, inlineWrite.Length);
        Assert.Equal(0x07, inlineWrite[42]);
        Assert.Equal(8, inlineWrite[43]);
        Assert.Equal(9, RobotBinaryBuffer.ReadUInt16LittleEndian(inlineWrite, 44));
        Assert.Equal(2, RobotBinaryBuffer.ReadUInt16LittleEndian(inlineWrite, 46));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, inlineWrite[48..52]);

        byte[] largePayload = new byte[10];
        largePayload[0] = 0xAA;
        largePayload[9] = 0xBB;
        byte[] extendedWrite = FanucRequestBuilder.BuildWriteRequest(FanucSelector.Data, 10, largePayload, 5);
        Assert.Equal(66, extendedWrite.Length);
        Assert.Equal(10, RobotBinaryBuffer.ReadUInt16LittleEndian(extendedWrite, 4));
        Assert.Equal(0x07, extendedWrite[50]);
        Assert.Equal(8, extendedWrite[51]);
        Assert.Equal(9, RobotBinaryBuffer.ReadUInt16LittleEndian(extendedWrite, 52));
        Assert.Equal(5, RobotBinaryBuffer.ReadUInt16LittleEndian(extendedWrite, 54));
        Assert.Equal(0xAA, extendedWrite[56]);
        Assert.Equal(0xBB, extendedWrite[65]);
    }

    [Fact]
    public void ResponseParser_InlineExtendedAndDeviceError_HandleAllCases()
    {
        byte[] inlineResponse = new byte[56];
        inlineResponse[31] = 0xD4;
        inlineResponse[44] = 0x11;
        inlineResponse[45] = 0x22;
        inlineResponse[46] = 0x33;
        inlineResponse[47] = 0x44;
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, FanucResponseParser.ParseReadResponse(inlineResponse, 4));

        byte[] extendedResponse = new byte[66];
        RobotBinaryBuffer.WriteUInt16LittleEndian(extendedResponse, 4, 10);
        extendedResponse[31] = 0x94;
        extendedResponse[56] = 0x55;
        extendedResponse[65] = 0x66;
        byte[] parsedExtended = FanucResponseParser.ParseReadResponse(extendedResponse, 10);
        Assert.Equal(0x55, parsedExtended[0]);
        Assert.Equal(0x66, parsedExtended[9]);

        byte[] errorResponse = new byte[56];
        errorResponse[31] = 0x77;
        RobotDeviceException deviceError = Assert.Throws<RobotDeviceException>(
            () => FanucResponseParser.ParseReadResponse(errorResponse, 4));
        Assert.Equal(0x77, deviceError.DeviceErrorCode);
    }

    [Fact]
    public async Task Client_ThreeStageHandshakeRunsOnceAndResetsOnReconnect()
    {
        var transport = CreateHandshakeAwareTransport();
        using var client = new FanucClient(transport);
        await client.OpenAsync();

        RobotResult<byte[]> firstRead = await client.ReadAddressWordsAsync("D1", 2);
        Assert.True(firstRead.IsSuccess);
        // 1 Connect + 1 SessionInit + 60 SETASG + 1 Read = 63
        Assert.Equal(63, transport.SentFrames.Count);
        Assert.Equal("CLRASG", ExtractSnpxCommand(transport.SentFrames[2]));
        Assert.Equal("SETASG 6159 2 $TIMER[10].$TIMER_VAL 1", ExtractSnpxCommand(transport.SentFrames[61]));

        RobotResult<byte[]> secondRead = await client.ReadAddressWordsAsync("D1", 2);
        Assert.True(secondRead.IsSuccess);
        Assert.Equal(64, transport.SentFrames.Count); // Handshake skipped on same connection

        transport.SimulateReconnect();
        RobotResult<byte[]> thirdRead = await client.ReadAddressWordsAsync("D1", 2);
        Assert.True(thirdRead.IsSuccess);
        Assert.Equal(64 + 63, transport.SentFrames.Count); // Re-executed handshake on new ConnectionId
    }

    [Fact]
    public async Task Client_WriteJointPose_UtOnly_WritesToWordAddressPlus46NotPlus45()
    {
        var transport = CreateHandshakeAwareTransport();
        using var client = new FanucClient(transport);
        await client.OpenAsync();

        float[] joint = new float[] { 1, 2, 3, 4, 5, 6, 0, 0, 0 };
        var pose = new FanucPose { Joint = joint, UserFrame = -1, UserTool = 3 };
        RobotResult result = await client.WriteJointPoseAsync(wordAddress: 100, pose);

        Assert.True(result.IsSuccess);

        // Last 3 writes: joint -> word 126, flag 0 -> word 144, ut 3 -> word 146
        byte[] jointWrite = transport.SentFrames[^3];
        byte[] flagWrite = transport.SentFrames[^2];
        byte[] utWrite = transport.SentFrames[^1];

        Assert.Equal(125, RobotBinaryBuffer.ReadUInt16LittleEndian(jointWrite, 52)); // 126 - 1 (extended)
        Assert.Equal(143, RobotBinaryBuffer.ReadUInt16LittleEndian(flagWrite, 44)); // 144 - 1 (inline)
        Assert.Equal(0, RobotBinaryBuffer.ReadInt16LittleEndian(flagWrite, 48));
        Assert.Equal(145, RobotBinaryBuffer.ReadUInt16LittleEndian(utWrite, 44)); // 146 - 1 (inline!)
        Assert.Equal(3, RobotBinaryBuffer.ReadInt16LittleEndian(utWrite, 48));
    }

    [Fact]
    public async Task Client_BitAlignmentAndSnapshotParsing_WorkEndToEnd()
    {
        var transport = CreateHandshakeAwareTransport(snapshotMode: true);
        using var client = new FanucClient(transport, new FanucOptions { SnapshotCacheDuration = TimeSpan.FromSeconds(5) });
        await client.OpenAsync();

        // Bit alignment: address 3, count 10 -> byteStartBit=1, byteEndBit=16, byteLength=2 -> bits [2..11]
        RobotResult<bool[]> bits = await client.ReadAddressBitsAsync("I3", 10);
        Assert.True(bits.IsSuccess);
        Assert.Equal(10, bits.Value.Length);
        Assert.True(bits.Value[0]); // bit 3 -> index 2 in 0x04

        RobotResult<FanucSnapshot> snapshot = await client.ReadSnapshotAsync();
        Assert.True(snapshot.IsSuccess);
        Assert.Equal(42, snapshot.Value.CurrentPose.UserFrame);
        Assert.Equal("MAIN", snapshot.Value.Task.ProgramName);
        Assert.Equal(123, snapshot.Value.FastClock);
        Assert.Equal(99.5f, snapshot.Value.DataPosRegMachineGroup[0].Xyzwpr[0]);

        RobotResult<bool[]> invalidBitSelector = await client.ReadBitsAsync(FanucSelector.Data, 1, 1);
        Assert.False(invalidBitSelector.IsSuccess);
        Assert.Equal(RobotErrorKind.Unsupported, invalidBitSelector.Error.Kind);
    }

    private static FakeTransport CreateHandshakeAwareTransport(bool snapshotMode = false)
    {
        var transport = new FakeTransport();
        int step = 0;

        transport.AutoResponder = frame =>
        {
            if (step < 2)
            {
                step++;
                return new byte[56];
            }

            bool isExtended = frame.Length > 56;
            byte service = isExtended ? frame[50] : frame[42];
            byte selector = isExtended ? frame[51] : frame[43];
            ushort len = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, isExtended ? 54 : 46);

            if (service == 0x07)
            {
                byte[] ack = new byte[56];
                ack[31] = 0xD4;
                return ack;
            }

            if (selector == (byte)FanucSelector.InputRelay)
            {
                byte[] bitResp = new byte[56];
                bitResp[31] = 0xD4;
                bitResp[44] = 0x04; // bit index 2 = true
                bitResp[45] = 0x01;
                return bitResp;
            }

            if (snapshotMode && len == 6130)
            {
                byte[] data = new byte[12260];
                Encoding.ASCII.GetBytes("MAIN\0").CopyTo(data, 2000);
                RobotBinaryBuffer.WriteInt16LittleEndian(data, 1400 + 90, 42); // CurrentPose.UF
                RobotBinaryBuffer.WriteInt32LittleEndian(data, 6544, 123); // FAST_CLOCK
                RobotBinaryBuffer.WriteSingleLittleEndian(data, 7440, 99.5f); // DataPosRegMG[0].Xyzwpr[0]

                byte[] extResp = new byte[56 + data.Length];
                RobotBinaryBuffer.WriteUInt16LittleEndian(extResp, 4, (ushort)data.Length);
                extResp[31] = 0x94;
                Array.Copy(data, 0, extResp, 56, data.Length);
                return extResp;
            }

            byte[] readResp = new byte[56];
            readResp[31] = 0xD4;
            readResp[44] = 0x12;
            readResp[45] = 0x34;
            return readResp;
        };

        return transport;
    }

    private static string ExtractSnpxCommand(byte[] frame)
    {
        bool isExtended = frame.Length > 56;
        if (isExtended)
        {
            ushort len = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, 4);
            return Encoding.ASCII.GetString(frame, 56, len);
        }

        ushort inlineLen = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, 46);
        return Encoding.ASCII.GetString(frame, 48, inlineLen);
    }
}
