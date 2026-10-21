using System;
using System.Text;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.LsElectric.FEnet;

public sealed class FEnetResponseParserTests
{
    [Fact]
    public void ParseContinuousRead_OfficialStandardResponse_ReturnsData()
    {
        byte[] payload = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        byte[] frame = MakeOfficialResponseFrame(
            invokeId: 1,
            command: 0x0055,
            errorState: 0,
            errorCode: 0,
            data: payload);

        byte[] result = FEnetResponseParser.ParseContinuousRead(frame, expectedInvokeId: 1);

        Assert.Equal(payload, result);
    }

    [Fact]
    public void ParseContinuousRead_LegacyResponse_ReturnsData()
    {
        byte[] payload = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        byte[] frame = MakeLegacyResponseFrame(
            invokeId: 1,
            command: 0x0055,
            errorState: 0,
            errorCode: 0,
            data: payload);

        byte[] result = FEnetResponseParser.ParseContinuousRead(frame, expectedInvokeId: 1);

        Assert.Equal(payload, result);
    }

    [Fact]
    public void ParseContinuousRead_WhenErrorStateRaised_ThrowsPlcDeviceException()
    {
        byte[] frame = MakeLegacyResponseFrame(
            invokeId: 1,
            command: 0x0055,
            errorState: 1,
            errorCode: (ushort)FEnetErrorCode.DeviceAreaError,
            data: Array.Empty<byte>());

        var ex = Assert.Throws<PlcDeviceException>(() =>
            FEnetResponseParser.ParseContinuousRead(frame, expectedInvokeId: 1));

        Assert.Equal((int)FEnetErrorCode.DeviceAreaError, ex.DeviceErrorCode);
    }

    [Fact]
    public void ParseContinuousWrite_ValidResponse_Succeeds()
    {
        byte[] frame = MakeLegacyResponseFrame(
            invokeId: 2,
            command: 0x0059,
            errorState: 0,
            errorCode: 0,
            data: Array.Empty<byte>());

        FEnetResponseParser.ParseContinuousWrite(frame, expectedInvokeId: 2);
    }

    [Fact]
    public void ValidateHeader_InvokeIdMismatch_ThrowsPlcException()
    {
        byte[] frame = MakeOfficialResponseFrame(
            invokeId: 1,
            command: 0x0055,
            errorState: 0,
            errorCode: 0,
            data: Array.Empty<byte>());

        var ex = Assert.Throws<PlcException>(() =>
            FEnetResponseParser.ParseContinuousRead(frame, expectedInvokeId: 2));

        Assert.Equal(PlcErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public void ValidateHeader_BccMismatch_ThrowsPlcException()
    {
        byte[] frame = MakeOfficialResponseFrame(
            invokeId: 1,
            command: 0x0055,
            errorState: 0,
            errorCode: 0,
            data: Array.Empty<byte>());

        frame[19] ^= 0xFF; // corrupt BCC

        var ex = Assert.Throws<PlcException>(() =>
            FEnetResponseParser.ParseContinuousRead(frame, expectedInvokeId: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, ex.Kind);
    }

    private static byte[] MakeOfficialResponseFrame(ushort invokeId, ushort command, ushort errorState, ushort errorCode, byte[] data)
    {
        ushort instructionLength = (ushort)(command == 0x0055 ? 12 + data.Length : 10);
        byte[] frame = new byte[20 + instructionLength];

        byte[] companyId = Encoding.ASCII.GetBytes("LSIS-XGT");
        Array.Copy(companyId, 0, frame, 0, 8);
        frame[12] = 0xA0; // CPU Info
        frame[13] = 0x11; // Source: Server -> Client
        frame[14] = (byte)invokeId;
        frame[15] = (byte)(invokeId >> 8);
        frame[16] = (byte)instructionLength;
        frame[17] = (byte)(instructionLength >> 8);

        byte bcc = 0;
        for (int i = 0; i < 19; i++)
        {
            bcc = (byte)(bcc + frame[i]);
        }

        frame[19] = bcc;

        // Instruction
        frame[20] = (byte)command;
        frame[21] = (byte)(command >> 8);
        frame[22] = 0x14; // continuous
        frame[23] = 0x00;
        frame[24] = 0x00; // reserved
        frame[25] = 0x00;
        frame[26] = (byte)errorState;
        frame[27] = (byte)(errorState >> 8);

        if (errorState != 0)
        {
            frame[28] = (byte)errorCode;
            frame[29] = (byte)(errorCode >> 8);
        }
        else if (command == 0x0055)
        {
            frame[28] = 0x01; // block count = 1
            frame[29] = 0x00;
            frame[30] = (byte)data.Length;
            frame[31] = (byte)(data.Length >> 8);
            Array.Copy(data, 0, frame, 32, data.Length);
        }

        return frame;
    }

    private static byte[] MakeLegacyResponseFrame(ushort invokeId, ushort command, ushort errorState, ushort errorCode, byte[] data)
    {
        ushort instructionLength = (ushort)(command == 0x0055 ? 14 + data.Length : 10);
        byte[] frame = new byte[20 + instructionLength];

        byte[] companyId = Encoding.ASCII.GetBytes("LSIS-XGT");
        Array.Copy(companyId, 0, frame, 0, 8);
        frame[12] = 0xA0; // CPU Info
        frame[13] = 0x11; // Source: Server -> Client
        frame[14] = (byte)invokeId;
        frame[15] = (byte)(invokeId >> 8);
        frame[16] = (byte)instructionLength;
        frame[17] = (byte)(instructionLength >> 8);

        byte bcc = 0;
        for (int i = 0; i < 19; i++)
        {
            bcc = (byte)(bcc + frame[i]);
        }

        frame[19] = bcc;

        // Instruction
        frame[20] = (byte)command;
        frame[21] = (byte)(command >> 8);
        frame[22] = 0x14; // continuous
        frame[23] = 0x00;
        frame[24] = 0x00; // reserved
        frame[25] = 0x00;
        frame[26] = (byte)errorState;
        frame[27] = (byte)(errorState >> 8);
        frame[28] = (byte)errorCode;
        frame[29] = (byte)(errorCode >> 8);

        if (command == 0x0055)
        {
            frame[30] = 0x01; // blocks
            frame[31] = 0x00;
            frame[32] = (byte)data.Length;
            frame[33] = (byte)(data.Length >> 8);
            Array.Copy(data, 0, frame, 34, data.Length);
        }

        return frame;
    }
}
