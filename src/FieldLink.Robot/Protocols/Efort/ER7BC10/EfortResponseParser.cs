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

        IEfortPacketLayout layout = version == EfortProtocolVersion.V2Aligned
            ? EfortV2AlignedLayout.Instance
            : EfortV1UnalignedLayout.Instance;

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
            PacketOrders = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset),
            PacketHeartbeat = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + 2),
            ErrorStatus = frame[headerOffset + 4],
            HstopStatus = frame[headerOffset + 5],
            AuthorityStatus = frame[headerOffset + 6],
            ServoStatus = frame[headerOffset + 7],
            AxisMoveStatus = frame[headerOffset + 8],
            ProgMoveStatus = frame[headerOffset + 9],
            ProgLoadStatus = frame[headerOffset + 10],
            ProgHoldStatus = frame[headerOffset + 11],
            ModeStatus = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + 12),
            SpeedStatus = RobotBinaryBuffer.ReadUInt16LittleEndian(frame, headerOffset + 14),
            DigitalOutputs = Array.AsReadOnly(ReadBytes(frame, headerOffset + 16, DigitalIoChannelCount)),
            DigitalInputs = Array.AsReadOnly(ReadBytes(frame, headerOffset + 48, DigitalIoChannelCount)),
            IntegerOutputs = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset, IntegerIoChannelCount)),
            IntegerInputs = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + 128, IntegerIoChannelCount)),
            ProjectName = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + 256, NameFieldLength, Encoding.ASCII),
            ProgramName = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + 288, NameFieldLength, Encoding.ASCII),
            ErrorText = RobotBinaryBuffer.ReadNullTrimmedString(frame, postPadOffset + 320, ErrorTextFieldLength, Encoding.ASCII),
            AxisPositions = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 448, AxisChannelCount)),
            CartesianPositions = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 476, CartesianAxisCount)),
            AxisSpeeds = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 500, AxisChannelCount)),
            AxisAccelerations = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 528, AxisChannelCount)),
            AxisJerks = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 556, AxisChannelCount)),
            AxisTorques = Array.AsReadOnly(ReadSingleArray(frame, postPadOffset + 584, AxisChannelCount)),
            AxisDirectionCounts = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + 612, AxisChannelCount)),
            AxisTimes = Array.AsReadOnly(ReadInt32Array(frame, postPadOffset + 640, AxisChannelCount)),
            DeviceTime = RobotBinaryBuffer.ReadInt32LittleEndian(frame, postPadOffset + 668),
            PacketEnd = RobotBinaryBuffer.ReadTrimmedAscii(frame, postPadOffset + 672, layout.PacketEndLength)
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
            result[i] = RobotBinaryBuffer.ReadInt32LittleEndian(frame, offset + (i * 4));
        }

        return result;
    }

    private static float[] ReadSingleArray(byte[] frame, int offset, int count)
    {
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadSingleLittleEndian(frame, offset + (i * 4));
        }

        return result;
    }
}
