using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;
using FieldLink.Robot.Tests.Fakes;
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
                return BuildYercResponse(requestId, 0x00, 0, 0, alarmPayload);
            }

            return BuildYercResponse(requestId, 0x00, 0, 0);
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
            return BuildYercResponse(frame[11], 0x00, 0, 0, payload);
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
            return BuildYercResponse(frame[11], 0x00, 0, 0, payload);
        };

        using var withoutHeaderClient = new YercUdpClient(withoutHeaderTransport, new YercUdpOptions { IncludeBatchCountHeader = false });
        await withoutHeaderClient.OpenAsync();
        RobotResult<ushort[]> regsWithoutHeader = await withoutHeaderClient.ReadRegisterBatchAsync(0, 2);
        Assert.True(regsWithoutHeader.IsSuccess);
        Assert.Equal(new ushort[] { 333, 444 }, regsWithoutHeader.Value);
    }

    private static byte[] BuildYercResponse(byte requestId, byte status, byte addedSize, ushort addedStatus, byte[]? payload = null)
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
