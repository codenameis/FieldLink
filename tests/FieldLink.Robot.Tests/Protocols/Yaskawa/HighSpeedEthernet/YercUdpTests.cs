using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;
using FieldLink.Robot.Tests.Fakes;
using FieldLink.Transport;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Yaskawa.HighSpeedEthernet;

public sealed class YercUdpTests
{
    [Fact]
    public void RequestBuilder_SpecVector2ReadStats_MatchesExact32Bytes()
    {
        // 출처: docs/specs/robot/08-yaskawa.md §8.3 Vector 2 ReadStats
        byte[] expected = FakeTransport.ParseHex(
            "59 45 52 43 20 00 00 00 03 01 00 01 00 00 00 00 " +
            "39 39 39 39 39 39 39 39 72 00 01 00 00 01 00 00");

        var header = new YercCommandHeader((ushort)YercCommand.ReadStats, 1, 0, (byte)YercService.GetAll);
        byte[] actual = YercRequestBuilder.BuildRequest(header, requestId: 1);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResponseParser_StatusAndExtendedAddedStatus_ThrowExpectedDeviceExceptions()
    {
        byte[] err08 = BuildYercResponse(requestId: 1, status: 0x08, addedSize: 0, addedStatus: 0);
        RobotDeviceException ex08 = Assert.Throws<RobotDeviceException>(() => YercResponseParser.ExtractPayload(err08, 1));
        Assert.Equal(0x08, ex08.DeviceErrorCode);

        byte[] err1F2B = BuildYercResponse(requestId: 1, status: 0x1F, addedSize: 2, addedStatus: 0x2010);
        RobotDeviceException ex2010 = Assert.Throws<RobotDeviceException>(() => YercResponseParser.ExtractPayload(err1F2B, 1));
        Assert.Equal(0x2010, ex2010.DeviceErrorCode);
        Assert.Contains("동작", ex2010.Message, StringComparison.Ordinal);

        byte[] err1F1B = BuildYercResponse(requestId: 1, status: 0x1F, addedSize: 1, addedStatus: 0x05);
        RobotDeviceException ex1B = Assert.Throws<RobotDeviceException>(() => YercResponseParser.ExtractPayload(err1F1B, 1));
        Assert.Equal(0x05, ex1B.DeviceErrorCode);
    }

    [Fact]
    public async Task Client_HistoryAlarmsAndSelectJob_FixSpecKnownBugs()
    {
        var transport = new FakeTransport();
        transport.AutoResponder = frame =>
        {
            byte requestId = frame[11];
            ushort command = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, 24);
            ushort instance = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, 26);

            if (command == (ushort)YercCommand.ReadHistoryAlarm)
            {
                byte[] alarmPayload = new byte[64];
                RobotBinaryBuffer.WriteInt32LittleEndian(alarmPayload, 0, instance);
                return BuildYercResponse(requestId, 0x00, 0, 0, alarmPayload, frame[29]);
            }

            return BuildYercResponse(requestId, 0x00, 0, 0, requestService: frame[29]);
        };

        using var client = new YercUdpClient(transport);
        await client.OpenAsync();

        RobotResult<IReadOnlyList<YercAlarmItem>> history = await client.ReadHistoryAlarmsAsync(startInstanceId: 1001, count: 3);
        Assert.True(history.IsSuccess);
        Assert.Equal(3, history.Value.Count);
        Assert.Equal(1001, history.Value[0].AlarmCode);
        Assert.Equal(1002, history.Value[1].AlarmCode);
        Assert.Equal(1003, history.Value[2].AlarmCode);

        RobotResult selectJob = await client.SelectJobAsync("MAIN_JOB", lineNumber: 5);
        Assert.True(selectJob.IsSuccess);
        byte[] selectFrame = transport.SentFrames[3];
        Assert.Equal(0x87, RobotBinaryBuffer.ReadUInt16LittleEndian(selectFrame, 24));
        Assert.Equal(1, RobotBinaryBuffer.ReadUInt16LittleEndian(selectFrame, 26));
        Assert.Equal(0, selectFrame[28]);
        Assert.Equal(0x02, selectFrame[29]);
        Assert.Equal(36, RobotBinaryBuffer.ReadUInt16LittleEndian(selectFrame, 6));
    }

    [Fact]
    public async Task Client_BatchReadWithAndWithoutCountHeader_RespectsOption()
    {
        var withHeaderTransport = new FakeTransport();
        withHeaderTransport.AutoResponder = frame =>
        {
            byte[] payload = new byte[4 + 4];
            RobotBinaryBuffer.WriteInt32LittleEndian(payload, 0, 2);
            RobotBinaryBuffer.WriteUInt16LittleEndian(payload, 4, 111);
            RobotBinaryBuffer.WriteUInt16LittleEndian(payload, 6, 222);
            return BuildYercResponse(frame[11], 0x00, 0, 0, payload, frame[29]);
        };

        using var withHeaderClient = new YercUdpClient(withHeaderTransport, new YercUdpOptions { IncludeBatchCountHeader = true });
        await withHeaderClient.OpenAsync();
        RobotResult<ushort[]> regsWithHeader = await withHeaderClient.ReadRegisterBatchAsync(0, 2);
        Assert.True(regsWithHeader.IsSuccess);
        Assert.Equal(new ushort[] { 111, 222 }, regsWithHeader.Value);

        var withoutHeaderTransport = new FakeTransport();
        withoutHeaderTransport.AutoResponder = frame =>
        {
            byte[] payload = new byte[4];
            RobotBinaryBuffer.WriteUInt16LittleEndian(payload, 0, 333);
            RobotBinaryBuffer.WriteUInt16LittleEndian(payload, 2, 444);
            return BuildYercResponse(frame[11], 0x00, 0, 0, payload, frame[29]);
        };

        using var withoutHeaderClient = new YercUdpClient(withoutHeaderTransport, new YercUdpOptions { IncludeBatchCountHeader = false });
        await withoutHeaderClient.OpenAsync();
        RobotResult<ushort[]> regsWithoutHeader = await withoutHeaderClient.ReadRegisterBatchAsync(0, 2);
        Assert.True(regsWithoutHeader.IsSuccess);
        Assert.Equal(new ushort[] { 333, 444 }, regsWithoutHeader.Value);
    }

    [Fact]
    public async Task Client_BatchWrites0x302To0x306_EncodeSpecHeadersAndPayloads()
    {
        var transport = new FakeTransport();
        transport.AutoResponder = frame => BuildYercResponse(frame[11], 0x00, 0, 0, requestService: frame[29]);

        using var client = new YercUdpClient(transport, new YercUdpOptions { IncludeBatchCountHeader = true });
        await client.OpenAsync();

        Assert.True((await client.WriteByteBatchAsync(10, new byte[] { 0x11, 0x22 })).IsSuccess);
        Assert.True((await client.WriteInt16BatchAsync(20, new short[] { -123, 456 })).IsSuccess);
        Assert.True((await client.WriteInt32BatchAsync(30, new[] { 100000, -200000 })).IsSuccess);
        Assert.True((await client.WriteSingleBatchAsync(40, new[] { 1.5f, -2.5f })).IsSuccess);
        Assert.True((await client.WriteStringBatchAsync(50, new[] { "AB", "CD" })).IsSuccess);

        Assert.Equal(5, transport.SentFrames.Count);

        byte[] byteFrame = transport.SentFrames[0];
        Assert.Equal((ushort)YercCommand.BatchByte, RobotBinaryBuffer.ReadUInt16LittleEndian(byteFrame, 24));
        Assert.Equal((byte)YercService.WriteMulti, byteFrame[29]);
        Assert.Equal(6, RobotBinaryBuffer.ReadUInt16LittleEndian(byteFrame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(byteFrame, 32));
        Assert.Equal(new byte[] { 0x11, 0x22 }, new[] { byteFrame[36], byteFrame[37] });

        byte[] int16Frame = transport.SentFrames[1];
        Assert.Equal((ushort)YercCommand.BatchInteger, RobotBinaryBuffer.ReadUInt16LittleEndian(int16Frame, 24));
        Assert.Equal(8, RobotBinaryBuffer.ReadUInt16LittleEndian(int16Frame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(int16Frame, 32));
        Assert.Equal((short)-123, RobotBinaryBuffer.ReadInt16LittleEndian(int16Frame, 36));
        Assert.Equal((short)456, RobotBinaryBuffer.ReadInt16LittleEndian(int16Frame, 38));

        byte[] int32Frame = transport.SentFrames[2];
        Assert.Equal((ushort)YercCommand.BatchDoubleInteger, RobotBinaryBuffer.ReadUInt16LittleEndian(int32Frame, 24));
        Assert.Equal(12, RobotBinaryBuffer.ReadUInt16LittleEndian(int32Frame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(int32Frame, 32));
        Assert.Equal(100000, RobotBinaryBuffer.ReadInt32LittleEndian(int32Frame, 36));
        Assert.Equal(-200000, RobotBinaryBuffer.ReadInt32LittleEndian(int32Frame, 40));

        byte[] singleFrame = transport.SentFrames[3];
        Assert.Equal((ushort)YercCommand.BatchReal, RobotBinaryBuffer.ReadUInt16LittleEndian(singleFrame, 24));
        Assert.Equal(12, RobotBinaryBuffer.ReadUInt16LittleEndian(singleFrame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(singleFrame, 32));
        Assert.Equal(1.5f, RobotBinaryBuffer.ReadSingleLittleEndian(singleFrame, 36));
        Assert.Equal(-2.5f, RobotBinaryBuffer.ReadSingleLittleEndian(singleFrame, 40));

        byte[] stringFrame = transport.SentFrames[4];
        Assert.Equal((ushort)YercCommand.BatchString, RobotBinaryBuffer.ReadUInt16LittleEndian(stringFrame, 24));
        Assert.Equal(36, RobotBinaryBuffer.ReadUInt16LittleEndian(stringFrame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(stringFrame, 32));
        Assert.Equal("AB", RobotBinaryBuffer.ReadNullTrimmedString(stringFrame, 36, 16, Encoding.ASCII));
        Assert.Equal("CD", RobotBinaryBuffer.ReadNullTrimmedString(stringFrame, 52, 16, Encoding.ASCII));

        var rawTransport = new FakeTransport();
        rawTransport.AutoResponder = frame => BuildYercResponse(frame[11], 0x00, 0, 0, requestService: frame[29]);

        using var rawClient = new YercUdpClient(rawTransport, new YercUdpOptions { IncludeBatchCountHeader = false });
        await rawClient.OpenAsync();
        Assert.True((await rawClient.WriteInt16BatchAsync(20, new short[] { 7 })).IsSuccess);
        Assert.Equal(2, RobotBinaryBuffer.ReadUInt16LittleEndian(rawTransport.SentFrames[0], 6));
        Assert.Equal((short)7, RobotBinaryBuffer.ReadInt16LittleEndian(rawTransport.SentFrames[0], 32));
    }

    [Fact]
    public async Task Client_BatchIo0x300_AlwaysUsesCountHeaderRegardlessOfOption()
    {
        var transport = new FakeTransport();
        transport.AutoResponder = frame =>
        {
            ushort command = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, 24);
            byte service = frame[29];
            if (command == (ushort)YercCommand.BatchIo && service == (byte)YercService.ReadMulti)
            {
                byte[] payload = new byte[4 + 2];
                RobotBinaryBuffer.WriteInt32LittleEndian(payload, 0, 2);
                payload[4] = 0xAA;
                payload[5] = 0x55;
                return BuildYercResponse(frame[11], 0x00, 0, 0, payload, service);
            }

            return BuildYercResponse(frame[11], 0x00, 0, 0, requestService: service);
        };

        using var client = new YercUdpClient(transport, new YercUdpOptions { IncludeBatchCountHeader = false });
        await client.OpenAsync();

        RobotResult<byte[]> readResult = await client.ReadIoBatchAsync(1, 2);
        Assert.True(readResult.IsSuccess);
        Assert.Equal(new byte[] { 0xAA, 0x55 }, readResult.Value);

        RobotResult writeResult = await client.WriteIoBatchAsync(1, new byte[] { 0x01, 0x02 });
        Assert.True(writeResult.IsSuccess);
        byte[] writeFrame = transport.SentFrames[1];
        Assert.Equal(6, RobotBinaryBuffer.ReadUInt16LittleEndian(writeFrame, 6));
        Assert.Equal(2, RobotBinaryBuffer.ReadInt32LittleEndian(writeFrame, 32));
        Assert.Equal(0x01, writeFrame[36]);
        Assert.Equal(0x02, writeFrame[37]);
    }

    [Fact]
    public async Task Client_ValidationChecks_RejectOddBytesNonFiniteFloatsAndInvalidRanges()
    {
        var transport = new FakeTransport();
        using var client = new YercUdpClient(transport);
        await client.OpenAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteIoBatchAsync(1, new byte[] { 0x01 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteByteBatchAsync(1, new byte[] { 0x01 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteSingleAsync(1, float.NaN));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteSingleBatchAsync(1, new[] { float.PositiveInfinity }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetCycleAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetCycleAsync(4));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SelectJobAsync(null!, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SelectJobAsync("MAIN", -1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SelectJobAsync(new string('A', 33), 0));
    }

    [Fact]
    public void ResponseParserAndDecoder_RejectInvalidHeaderSizeDataLengthAckAndService()
    {
        byte[] valid = BuildYercResponse(requestId: 5, status: 0x00, addedSize: 0, addedStatus: 0, payload: new byte[] { 0x42 }, requestService: (byte)YercService.GetSingle);

        byte[] badHeaderSize = (byte[])valid.Clone();
        RobotBinaryBuffer.WriteUInt16LittleEndian(badHeaderSize, 4, 30);
        RobotException exHeaderSize = Assert.Throws<RobotException>(() => YercResponseParser.ExtractPayload(badHeaderSize, 5, (byte)YercService.GetSingle));
        Assert.Equal(RobotErrorKind.InvalidResponse, exHeaderSize.Kind);

        var decoder = new YercFrameDecoder();
        Assert.Throws<TransportException>(() => decoder.TryGetFrameLength(new ArraySegment<byte>(badHeaderSize), out _));

        byte[] badDataLength = (byte[])valid.Clone();
        RobotBinaryBuffer.WriteUInt16LittleEndian(badDataLength, 6, 9);
        RobotException exDataLength = Assert.Throws<RobotException>(() => YercResponseParser.ExtractPayload(badDataLength, 5, (byte)YercService.GetSingle));
        Assert.Equal(RobotErrorKind.InvalidResponse, exDataLength.Kind);

        byte[] missingAck = (byte[])valid.Clone();
        missingAck[10] = 0x00;
        RobotException exAck = Assert.Throws<RobotException>(() => YercResponseParser.ExtractPayload(missingAck, 5, (byte)YercService.GetSingle));
        Assert.Equal(RobotErrorKind.InvalidResponse, exAck.Kind);

        byte[] wrongService = (byte[])valid.Clone();
        wrongService[24] = (byte)YercService.SetSingle | 0x80;
        RobotException exService = Assert.Throws<RobotException>(() => YercResponseParser.ExtractPayload(wrongService, 5, (byte)YercService.GetSingle));
        Assert.Equal(RobotErrorKind.InvalidResponse, exService.Kind);
    }

    private static byte[] BuildYercResponse(
        byte requestId,
        byte status,
        byte addedSize,
        ushort addedStatus,
        byte[]? payload = null,
        byte requestService = (byte)YercService.GetAll)
    {
        int payloadLen = payload?.Length ?? 0;
        byte[] response = new byte[32 + payloadLen];
        response[0] = (byte)'Y';
        response[1] = (byte)'E';
        response[2] = (byte)'R';
        response[3] = (byte)'C';
        RobotBinaryBuffer.WriteUInt16LittleEndian(response, 4, 32);
        RobotBinaryBuffer.WriteUInt16LittleEndian(response, 6, (ushort)payloadLen);
        response[10] = 0x01;
        response[11] = requestId;
        response[24] = (byte)(requestService | 0x80);
        response[25] = status;
        response[26] = addedSize;
        if (addedSize == 1)
        {
            response[28] = (byte)addedStatus;
        }
        else
        {
            RobotBinaryBuffer.WriteUInt16LittleEndian(response, 28, addedStatus);
        }

        if (payload is { Length: > 0 })
        {
            Array.Copy(payload, 0, response, 32, payloadLen);
        }

        return response;
    }
}
