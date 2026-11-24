using System;
using System.Text;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Estun.Modbus;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Estun.Modbus;

public sealed class EstunTests
{
    [Fact]
    public void RequestBuilder_SpecVectors_MatchExactBytes()
    {
        // 출처: docs/specs/robot/03-estun-tcp.md §9.2 Vector 1 (ReadRobotData TxID=1, Unit=1, Reg=0, Qty=100)
        byte[] expectedRead = FakeTransport.ParseHex("00 01 00 00 00 06 01 03 00 00 00 64");
        Assert.Equal(expectedRead, EstunModbusRequestBuilder.BuildReadHoldingRegisters(1, 0x01, 0, 100));

        // 출처: docs/specs/robot/03-estun-tcp.md §9.2 Vector 2 (Write Reg 99 = 0x0011, TxID=2, Unit=1)
        byte[] expectedWrite = FakeTransport.ParseHex("00 02 00 00 00 06 01 06 00 63 00 11");
        Assert.Equal(expectedWrite, EstunModbusRequestBuilder.BuildWriteSingleRegister(2, 0x01, 99, 0x0011));
    }

    [Fact]
    public void SnapshotParser_SpecVectorCdabWordSwapAndFlags_DecodeAccurately()
    {
        byte[] registerBytes = new byte[200];
        RobotBinaryBuffer.WriteInt16BigEndian(registerBytes, 2, 75); // GlobalSpeedValue
        registerBytes[7] = 0b1010_1010; // AutoMode(1), EnableStatus(3), ErrorStatus(5), RobotMoving(7)

        // ProjectName "ABCD" stored on wire as "BADC"
        byte[] swappedName = RobotBinaryBuffer.ReverseByWord(Encoding.ASCII.GetBytes("ABCD\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0"), 0, 20);
        Array.Copy(swappedName, 0, registerBytes, 8, 20);

        // DO bit 0 and bit 9 -> swapped word: bit 0 is in byte[1] bit0, bit 9 is in byte[0] bit1
        byte[] doBytes = new byte[8];
        doBytes[0] = 0x01; // bit 0
        doBytes[1] = 0x02; // bit 9
        byte[] wireDo = RobotBinaryBuffer.ReverseByWord(doBytes, 0, 8);
        Array.Copy(wireDo, 0, registerBytes, 28, 8);

        RobotBinaryBuffer.WriteUInt16BigEndian(registerBytes, 36, 0x0801); // RobotCommandStatus

        // 출처: docs/specs/robot/03-estun-tcp.md §9.2 Vector 3 (AO[0] = 1.0f -> 00 00 3F 80)
        registerBytes[38] = 0x00;
        registerBytes[39] = 0x00;
        registerBytes[40] = 0x3F;
        registerBytes[41] = 0x80;

        // AI[15] = 1.0f
        registerBytes[194] = 0x00;
        registerBytes[195] = 0x00;
        registerBytes[196] = 0x3F;
        registerBytes[197] = 0x80;

        RobotBinaryBuffer.WriteInt16BigEndian(registerBytes, 198, 0x0011);

        EstunRobotSnapshot snapshot = EstunSnapshotParser.ParseSnapshot(registerBytes);

        Assert.Equal(75, snapshot.GlobalSpeedValue);
        Assert.False(snapshot.IsManualMode);
        Assert.True(snapshot.IsAutoMode);
        Assert.False(snapshot.IsRemoteMode);
        Assert.True(snapshot.IsServoEnabled);
        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.HasError);
        Assert.False(snapshot.IsProgramRunning);
        Assert.True(snapshot.IsMoving);
        Assert.Equal("ABCD", snapshot.ProjectName);
        Assert.True(snapshot.DigitalOutputs[0]);
        Assert.True(snapshot.DigitalOutputs[9]);
        Assert.False(snapshot.DigitalOutputs[1]);
        Assert.Equal(0x0801, snapshot.RobotCommandStatus);
        Assert.Equal(1.0f, snapshot.AnalogOutputs[0]);
        Assert.Equal(1.0f, snapshot.AnalogInputs[15]);
        Assert.Equal(0x0011, snapshot.ReadWriteFlag);
    }

    [Fact]
    public async Task Client_NonDefaultStationAndHandshake_ExecuteAll6Steps()
    {
        var transport = new FakeTransport();
        byte station = 0x05;
        int pollCount = 0;

        transport.AutoResponder = frame =>
        {
            ushort txId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 0);
            Assert.Equal(station, frame[6]);
            byte fc = frame[7];
            ushort address = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 8);

            if (fc == 0x03)
            {
                if (address == 18)
                {
                    pollCount++;
                    // First poll returns 0x0000, second returns 0x0801, third (Step 5) returns 0x0801
                    ushort status = pollCount == 1 ? (ushort)0x0000 : (ushort)0x0801;
                    return BuildReadRegisterResponse(txId, station, status);
                }

                return BuildReadRegisterResponse(txId, station, 0);
            }

            return BuildWriteEchoResponse(txId, station, frame);
        };

        var options = new EstunOptions
        {
            Station = station,
            KeepAliveInterval = TimeSpan.Zero,
            HandshakePollInterval = TimeSpan.FromMilliseconds(1)
        };

        using var client = new EstunClient(transport, options);
        await client.OpenAsync();

        RobotResult result = await client.LoadProjectAsync("MY_JOB");
        Assert.True(result.IsSuccess);

        // Pre-write Reg 53 (FC 0x10), Step 1 (Read 99, Read 51), Step 2 (Write 99=0x0011),
        // Step 3 (Read 18 x2), Step 4 (Write 51=0x0080), Step 5 (Read 18), Step 6a (Write 99=0), Step 6b (Write 51=0)
        Assert.Equal(10, transport.SentFrames.Count);
        Assert.Equal(0x10, transport.SentFrames[0][7]); // LoadProject name write
        Assert.Equal(53, RobotBinaryBuffer.ReadUInt16BigEndian(transport.SentFrames[0], 8));
        Assert.Equal(0x0080, RobotBinaryBuffer.ReadInt16BigEndian(transport.SentFrames[6], 10)); // Step 4 command
    }

    [Fact]
    public async Task Client_WhenStep1BusyOrStep3TimesOut_AbortsOrReturnsTimeout()
    {
        var transport = new FakeTransport();
        transport.AutoResponder = frame =>
        {
            ushort txId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 0);
            ushort addr = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 8);
            ushort value = addr == 99 ? (ushort)0x0011 : (ushort)0x0000;
            return BuildReadRegisterResponse(txId, 0x01, value);
        };

        using var busyClient = new EstunClient(transport, new EstunOptions { KeepAliveInterval = TimeSpan.Zero });
        await busyClient.OpenAsync();

        RobotResult busyResult = await busyClient.StartProgramAsync();
        Assert.False(busyResult.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, busyResult.Error.Kind);
        Assert.Single(transport.SentFrames); // Aborted after Reg 99 != 0 without writing Reg 99

        var timeoutTransport = new FakeTransport();
        timeoutTransport.AutoResponder = frame =>
        {
            ushort txId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 0);
            if (frame[7] == 0x03)
            {
                return BuildReadRegisterResponse(txId, 0x01, 0x0000);
            }

            return BuildWriteEchoResponse(txId, 0x01, frame);
        };

        using var timeoutClient = new EstunClient(timeoutTransport, new EstunOptions
        {
            KeepAliveInterval = TimeSpan.Zero,
            HandshakePollInterval = TimeSpan.FromMilliseconds(1),
            MaxAuthorityPollAttempts = 2
        });
        await timeoutClient.OpenAsync();

        RobotResult timeoutResult = await timeoutClient.StartProgramAsync();
        Assert.False(timeoutResult.IsSuccess);
        Assert.Equal(RobotErrorKind.Timeout, timeoutResult.Error.Kind);
    }

    [Fact]
    public async Task Client_ModbusExceptionResponse_MapsToDeviceError()
    {
        var transport = new FakeTransport();
        transport.AutoResponder = frame => new byte[]
        {
            frame[0], frame[1], 0x00, 0x00, 0x00, 0x03, frame[6], (byte)(frame[7] | 0x80), 0x02
        };

        using var client = new EstunClient(transport, new EstunOptions { KeepAliveInterval = TimeSpan.Zero });
        await client.OpenAsync();

        RobotResult<EstunRobotSnapshot> result = await client.ReadSnapshotAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, result.Error.Kind);
        Assert.Equal(0x02, result.Error.DeviceErrorCode);
    }

    private static byte[] BuildReadRegisterResponse(ushort txId, byte station, ushort value)
    {
        byte[] response = new byte[11];
        RobotBinaryBuffer.WriteUInt16BigEndian(response, 0, txId);
        RobotBinaryBuffer.WriteUInt16BigEndian(response, 4, 5);
        response[6] = station;
        response[7] = 0x03;
        response[8] = 2;
        RobotBinaryBuffer.WriteUInt16BigEndian(response, 9, value);
        return response;
    }

    private static byte[] BuildWriteEchoResponse(ushort txId, byte station, byte[] request)
    {
        byte[] response = new byte[12];
        RobotBinaryBuffer.WriteUInt16BigEndian(response, 0, txId);
        RobotBinaryBuffer.WriteUInt16BigEndian(response, 4, 6);
        response[6] = station;
        response[7] = request[7];
        response[8] = request[8];
        response[9] = request[9];
        response[10] = request[10];
        response[11] = request[11];
        return response;
    }
}
