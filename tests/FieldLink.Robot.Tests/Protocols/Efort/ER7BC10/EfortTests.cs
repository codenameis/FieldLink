using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Efort.ER7BC10;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Efort.ER7BC10;

public sealed class EfortTests
{
    [Fact]
    public void RequestBuilder_SpecVectors_MatchExactBytes()
    {
        // 출처: docs/specs/robot/02-efort-er7bc10.md §9.2 Vector 1 (v2, seq=1, 38B)
        byte[] expectedV2 = FakeTransport.ParseHex(
            "4D 65 73 73 61 67 65 48 65 61 64 00 00 00 00 00 " +
            "26 00 E9 03 01 00 " +
            "4D 65 73 73 61 67 65 54 61 69 6C 00 00 00 00 00");

        byte[] actualV2 = EfortRequestBuilder.BuildReadCommand(1, EfortProtocolVersion.V2Aligned);
        Assert.Equal(expectedV2, actualV2);

        // 출처: docs/specs/robot/02-efort-er7bc10.md §9.2 Vector 2 (v1, seq=1, 36B)
        byte[] expectedV1 = FakeTransport.ParseHex(
            "4D 65 73 73 61 67 65 48 65 61 64 00 00 00 00 " +
            "24 00 E9 03 01 00 " +
            "4D 65 73 73 61 67 65 54 61 69 6C 00 00 00 00");

        byte[] actualV1 = EfortRequestBuilder.BuildReadCommand(1, EfortProtocolVersion.V1Unaligned);
        Assert.Equal(expectedV1, actualV1);
    }

    [Fact]
    public void ResponseParser_V2AlignedAndV1Unaligned_DecodeExactOffsetsAndAlignmentJump()
    {
        byte[] v2Frame = BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned);
        EfortRobotSnapshot v2 = EfortResponseParser.ParseSnapshot(v2Frame, EfortProtocolVersion.V2Aligned);

        Assert.Equal("MessageHead", v2.PacketStart);
        Assert.Equal(1001, v2.PacketOrders);
        Assert.Equal(77, v2.PacketHeartbeat);
        Assert.Equal(1, v2.ErrorStatus);
        Assert.Equal(0, v2.HstopStatus);
        Assert.Equal(2, v2.ModeStatus);
        Assert.Equal(85, v2.SpeedStatus);
        Assert.Equal(0xAA, v2.DigitalOutputs[0]);
        Assert.Equal(0xBB, v2.DigitalInputs[31]);
        Assert.Equal(123456, v2.IntegerOutputs[0]);
        Assert.Equal(-654321, v2.IntegerInputs[31]);
        Assert.Equal("CELL_PROJECT", v2.ProjectName);
        Assert.Equal("PICK_PLACE", v2.ProgramName);
        Assert.Equal("Servo Warning", v2.ErrorText);
        Assert.Equal(12.5f, v2.AxisPositions[0]);
        Assert.Equal(-45.25f, v2.CartesianPositions[5]);
        Assert.Equal(9999, v2.DeviceTime);
        Assert.Equal("MessageTail", v2.PacketEnd);

        byte[] v1Frame = BuildMockSnapshotFrame(EfortProtocolVersion.V1Unaligned);
        EfortRobotSnapshot v1 = EfortResponseParser.ParseSnapshot(v1Frame, EfortProtocolVersion.V1Unaligned);

        Assert.Equal(123456, v1.IntegerOutputs[0]);
        Assert.Equal(-654321, v1.IntegerInputs[31]);
        Assert.Equal("CELL_PROJECT", v1.ProjectName);
        Assert.Equal("MessageTail", v1.PacketEnd);
    }

    [Fact]
    public void ResponseParser_WhenTooShort_ThrowsInvalidResponse()
    {
        byte[] shortFrame = new byte[100];
        RobotException error = Assert.Throws<RobotException>(
            () => EfortResponseParser.ParseSnapshot(shortFrame, EfortProtocolVersion.V2Aligned));

        Assert.Equal(RobotErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public void FrameDecoder_HandlesPartialHeaderNegativeLengthAndCompleteFrame()
    {
        var decoder = new EfortFrameDecoder(EfortProtocolVersion.V2Aligned);
        byte[] fullFrame = BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned);

        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(fullFrame, 0, 17), out _));
        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(fullFrame, 0, 100), out _));
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(fullFrame), out int len));
        Assert.Equal(788, len);

        byte[] negativeLengthHeader = new byte[18];
        RobotBinaryBuffer.WriteInt16LittleEndian(negativeLengthHeader, 16, 5);
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(negativeLengthHeader), out int clamped));
        Assert.Equal(18, clamped);
    }

    [Fact]
    public async Task Client_ReadsSnapshotIncrementsHeartbeatResetsOnReconnectAndRejectsWrites()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncoming(BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned));
        transport.EnqueueIncoming(BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned));

        using var client = new EfortClient(transport, new EfortOptions { Version = EfortProtocolVersion.V2Aligned });
        await client.OpenAsync();

        RobotResult<EfortRobotSnapshot> first = await client.ReadSnapshotAsync();
        Assert.True(first.IsSuccess);
        Assert.Equal(0, RobotBinaryBuffer.ReadUInt16LittleEndian(transport.SentFrames[0], 20));

        RobotResult<string> json = await client.ReadStringAsync(string.Empty);
        Assert.True(json.IsSuccess);
        Assert.Contains("\"ProjectName\":\"CELL_PROJECT\"", json.Value, StringComparison.Ordinal);
        Assert.Equal(1, RobotBinaryBuffer.ReadUInt16LittleEndian(transport.SentFrames[1], 20));

        transport.SimulateReconnect();
        transport.EnqueueIncoming(BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned));
        await client.ReadSnapshotAsync();
        Assert.Equal(0, RobotBinaryBuffer.ReadUInt16LittleEndian(transport.SentFrames[2], 20));

        RobotResult writeResult = await client.WriteStringAsync("any", "val");
        Assert.False(writeResult.IsSuccess);
        Assert.Equal(RobotErrorKind.Unsupported, writeResult.Error.Kind);
    }

    [Fact]
    public async Task Client_WhenSnapshotContainsNaN_ReadStringReturnsParseErrorAndToJsonIncludesAllFields()
    {
        byte[] validFrame = BuildMockSnapshotFrame(EfortProtocolVersion.V2Aligned);
        EfortRobotSnapshot fullSnapshot = EfortResponseParser.ParseSnapshot(validFrame, EfortProtocolVersion.V2Aligned);
        string fullJson = fullSnapshot.ToJson();
        Assert.Contains("\"PacketStart\":", fullJson, StringComparison.Ordinal);
        Assert.Contains("\"DigitalOutputs\":", fullJson, StringComparison.Ordinal);
        Assert.Contains("\"AxisPositions\":", fullJson, StringComparison.Ordinal);
        Assert.Contains("\"CartesianPositions\":", fullJson, StringComparison.Ordinal);
        Assert.Contains("\"PacketEnd\":", fullJson, StringComparison.Ordinal);

        Assert.NotSame(
            EfortOptions.CreateLayout(EfortProtocolVersion.V2Aligned),
            EfortOptions.CreateLayout(EfortProtocolVersion.V2Aligned));

        byte[] nanFrame = (byte[])validFrame.Clone();
        // V2Aligned postPaddingShift=100, DbAxisPos offset=448 -> byte 548
        RobotBinaryBuffer.WriteSingleLittleEndian(nanFrame, 100 + 448, float.NaN);

        var transport = new FakeTransport();
        transport.EnqueueIncoming(nanFrame);

        using var client = new EfortClient(transport, new EfortOptions { Version = EfortProtocolVersion.V2Aligned });
        await client.OpenAsync();

        RobotResult<string> jsonResult = await client.ReadStringAsync(string.Empty);
        Assert.False(jsonResult.IsSuccess);
        Assert.Equal(RobotErrorKind.ParseError, jsonResult.Error.Kind);
    }

    private static byte[] BuildMockSnapshotFrame(EfortProtocolVersion version)
    {
        bool isV2 = version == EfortProtocolVersion.V2Aligned;
        int totalLength = isV2 ? 788 : 784;
        int headerShift = isV2 ? 18 : 17;
        int postPaddingShift = isV2 ? 100 : 97;
        int headTailLength = isV2 ? 16 : 15;

        byte[] frame = new byte[totalLength];
        Encoding.ASCII.GetBytes("MessageHead ").CopyTo(frame, 0);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, headTailLength, (ushort)totalLength);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, headerShift, 1001);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, headerShift + 2, 77);
        frame[headerShift + 4] = 1; // ErrorStatus
        frame[headerShift + 5] = 0; // HstopStatus (E-Stop)
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, headerShift + 12, 2); // ModeStatus
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, headerShift + 14, 85); // SpeedStatus
        frame[headerShift + 16] = 0xAA; // IoDOut[0]
        frame[headerShift + 48 + 31] = 0xBB; // IoDIn[31]

        RobotBinaryBuffer.WriteInt32LittleEndian(frame, postPaddingShift, 123456); // IoIOut[0]
        RobotBinaryBuffer.WriteInt32LittleEndian(frame, postPaddingShift + 128 + (31 * 4), -654321); // IoIIn[31]
        Encoding.ASCII.GetBytes("CELL_PROJECT\0").CopyTo(frame, postPaddingShift + 256);
        Encoding.ASCII.GetBytes("PICK_PLACE\0").CopyTo(frame, postPaddingShift + 288);
        Encoding.ASCII.GetBytes("Servo Warning\0").CopyTo(frame, postPaddingShift + 320);
        RobotBinaryBuffer.WriteSingleLittleEndian(frame, postPaddingShift + 448, 12.5f); // DbAxisPos[0]
        RobotBinaryBuffer.WriteSingleLittleEndian(frame, postPaddingShift + 476 + (5 * 4), -45.25f); // DbCartPos[5]
        RobotBinaryBuffer.WriteInt32LittleEndian(frame, postPaddingShift + 668, 9999); // DbDeviceTime
        Encoding.ASCII.GetBytes(" MessageTail").CopyTo(frame, postPaddingShift + 672);

        return frame;
    }
}
