using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 응답 프레임을 스냅샷 객체로 해석하는 순수 정적 파서입니다.</summary>
public static class EfortResponseParser
{
    private const int DigitalIoChannelCount = 32;
    private const int IntegerIoChannelCount = 32;
    private const int AxisChannelCount = 7;
    private const int CartesianAxisCount = 6;
    private const int NameFieldLength = 32;
    private const int ErrorTextFieldLength = 128;
    private const int Int32ByteSize = sizeof(int);
    private const int SingleByteSize = sizeof(float);

    private const int PacketOrdersOffset = 0;
    private const int PacketHeartbeatOffset = 2;
    private const int ErrorStatusOffset = 4;
    private const int HstopStatusOffset = 5;
    private const int AuthorityStatusOffset = 6;
    private const int ServoStatusOffset = 7;
    private const int AxisMoveStatusOffset = 8;
    private const int ProgMoveStatusOffset = 9;
    private const int ProgLoadStatusOffset = 10;
    private const int ProgHoldStatusOffset = 11;
    private const int ModeStatusOffset = 12;
    private const int SpeedStatusOffset = 14;
    private const int DigitalOutputsOffset = 16;
    private const int DigitalInputsOffset = 48;

    private const int IntegerOutputsOffset = 0;
    private const int IntegerInputsOffset = 128;
    private const int ProjectNameOffset = 256;
    private const int ProgramNameOffset = 288;
    private const int ErrorTextOffset = 320;
    private const int AxisPositionsOffset = 448;
    private const int CartesianPositionsOffset = 476;
    private const int AxisSpeedsOffset = 500;
    private const int AxisAccelerationsOffset = 528;
    private const int AxisJerksOffset = 556;
    private const int AxisTorquesOffset = 584;
    private const int AxisDirectionCountsOffset = 612;
    private const int AxisTimesOffset = 640;
    private const int DeviceTimeOffset = 668;
    private const int PacketEndOffset = 672;

    /// <summary>EFORT 바이너리 상태 응답 프레임을 <see cref="EfortRobotSnapshot"/>으로 파싱합니다.</summary>
    /// <param name="frame">수신된 응답 바이트 배열입니다.</param>
    /// <param name="version">v2 정렬 또는 v1 비정렬 버전입니다.</param>
    /// <returns>해석된 로봇 상태 스냅샷입니다.</returns>
    /// <exception cref="RobotException">프레임 길이가 최소 규격보다 짧을 때.</exception>
    public static EfortRobotSnapshot ParseSnapshot(byte[] frame, EfortProtocolVersion version = EfortProtocolVersion.V2Aligned)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        IEfortPacketLayout layout = EfortOptions.CreateLayout(version);
        if (frame.Length < layout.MinimumResponseLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"EFORT 응답 길이가 최소 크기({layout.MinimumResponseLength} 바이트)보다 짧습니다. (실제: {frame.Length} 바이트)",
                frame);
        }

        int headerOffset = layout.HeaderLength;
        int postPadOffset = layout.PostDigitalInputOffset;

        return new EfortRobotSnapshot
        {
            PacketStart = RobotBinaryBuffer.ReadTrimmedAscii(frame, 0, layout.PacketStartLength),
            PacketOrders = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + PacketOrdersOffset),
            PacketHeartbeat = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + PacketHeartbeatOffset),
            ErrorStatus = frame[headerOffset + ErrorStatusOffset],
            HstopStatus = frame[headerOffset + HstopStatusOffset],
            AuthorityStatus = frame[headerOffset + AuthorityStatusOffset],
            ServoStatus = frame[headerOffset + ServoStatusOffset],
            AxisMoveStatus = frame[headerOffset + AxisMoveStatusOffset],
            ProgMoveStatus = frame[headerOffset + ProgMoveStatusOffset],
            ProgLoadStatus = frame[headerOffset + ProgLoadStatusOffset],
            ProgHoldStatus = frame[headerOffset + ProgHoldStatusOffset],
            ModeStatus = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + ModeStatusOffset),
            SpeedStatus = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + SpeedStatusOffset),
            DigitalOutputs = Array.AsReadOnly(ReadBytes(frame, headerOffset + DigitalOutputsOffset, DigitalIoChannelCount)),
            DigitalInputs = Array.AsReadOnly(ReadBytes(frame, headerOffset + DigitalInputsOffset, DigitalIoChannelCount)),
            IntegerOutputs = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + IntegerOutputsOffset, IntegerIoChannelCount)),
            IntegerInputs = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + IntegerInputsOffset, IntegerIoChannelCount)),
            ProjectName = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + ProjectNameOffset, NameFieldLength, Encoding.ASCII),
            ProgramName = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + ProgramNameOffset, NameFieldLength, Encoding.ASCII),
            ErrorText = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + ErrorTextOffset, ErrorTextFieldLength, Encoding.ASCII),
            AxisPositions = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + AxisPositionsOffset, AxisChannelCount)),
            CartesianPositions = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + CartesianPositionsOffset, CartesianAxisCount)),
            AxisSpeeds = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + AxisSpeedsOffset, AxisChannelCount)),
            AxisAccelerations = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + AxisAccelerationsOffset, AxisChannelCount)),
            AxisJerks = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + AxisJerksOffset, AxisChannelCount)),
            AxisTorques = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + AxisTorquesOffset, AxisChannelCount)),
            AxisDirectionCounts = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + AxisDirectionCountsOffset, AxisChannelCount)),
            AxisTimes = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + AxisTimesOffset, AxisChannelCount)),
            DeviceTime = RobotBinaryBuffer.ReadInt32LittleEndian(frame, postPadOffset + DeviceTimeOffset),
            PacketEnd = RobotBinaryBuffer.ReadTrimmedAscii(frame, postPadOffset + PacketEndOffset, layout.PacketEndLength)
        };
    }

    private static byte[] ReadBytes(byte[] frame, int offset, int count)
    {
        byte[] result = new byte[count];
        Array.Copy(frame, offset, result, 0, count);
        return result;
    }

    private static int[] ReadInt32Array(byte[] frame, int offset, int count)
    {
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadInt32LittleEndian(frame, offset + (i * Int32ByteSize));
        }

        return result;
    }

    private static float[] ReadSingleArray(byte[] frame, int offset, int count)
    {
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadSingleLittleEndian(frame, offset + (i * SingleByteSize));
        }

        return result;
    }
}
