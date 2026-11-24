using System;
using System.Collections.Generic;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC SRTP/SNPX 응답 프레임과 전체 스냅샷을 해석하는 순수 정적 파서입니다.</summary>
public static class FanucResponseParser
{
    private const int HeaderLength = 56;
    private const int InlinePayloadOffset = 44;
    private const int InlineMaxBytes = 6;
    private const byte ExtendedReadSuccessCode = 0x94;
    private const byte InlineSuccessCode = 0xD4;
    private const int StatusByteOffset = 31;
    private const int RequiredSnapshotByteLength = 12260;

    /// <summary>FANUC 읽기 응답 프레임에서 요청 바이트 수만큼의 페이로드를 추출합니다.</summary>
    public static byte[] ParseReadResponse(byte[] response, int expectedByteCount)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.Length < HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 응답 길이가 헤더 크기(56 바이트)보다 짧습니다. (실제: {response.Length} 바이트)",
                response);
        }

        byte status = response[StatusByteOffset];
        if (status == InlineSuccessCode)
        {
            return ExtractInlinePayload(response, expectedByteCount);
        }

        if (status == ExtendedReadSuccessCode)
        {
            return ExtractExtendedPayload(response, expectedByteCount);
        }

        throw new RobotDeviceException(
            status,
            $"FANUC 읽기 요청이 장치 오류(0x{status:X2})로 거절되었습니다.",
            response);
    }

    /// <summary>FANUC 쓰기 응답 프레임의 성공 여부를 검증합니다.</summary>
    public static void ParseWriteResponse(byte[] response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.Length < HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 쓰기 응답 길이가 헤더 크기(56 바이트)보다 짧습니다. (실제: {response.Length} 바이트)",
                response);
        }

        byte status = response[StatusByteOffset];
        if (status != InlineSuccessCode)
        {
            throw new RobotDeviceException(
                status,
                $"FANUC 쓰기 요청이 장치 오류(0x{status:X2})로 거절되었습니다.",
                response);
        }
    }

    /// <summary><c>D1</c> 기준 6130워드(12,260바이트) 스냅샷 블록을 <see cref="FanucSnapshot"/>으로 파싱합니다.</summary>
    public static FanucSnapshot ParseSnapshot(byte[] data, Encoding encoding)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        if (data.Length < RequiredSnapshotByteLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 스냅샷 데이터 길이가 12,260바이트보다 짧습니다. (실제: {data.Length} 바이트)",
                data);
        }

        return new FanucSnapshot
        {
            AlarmList = ParseAlarms(data, 0, 5, encoding),
            AlarmCurrent = ParseAlarm(data, 1000, encoding),
            AlarmPassword = ParseAlarm(data, 1200, encoding),
            CurrentPose = ParsePose(data, 1400),
            CurrentPoseUserFrame = ParsePose(data, 1500),
            GroupCurrentPoses = ParsePoses(data, 1600, 4),
            Task = ParseTask(data, 2000, encoding),
            TaskIgnoreMacro = ParseTask(data, 2036, encoding),
            TaskIgnoreKarel = ParseTask(data, 2072, encoding),
            TaskIgnoreMacroKarel = ParseTask(data, 2108, encoding),
            PosRegGroup1 = ParsePoses(data, 2144, 10),
            PosRegGroup2 = ParsePoses(data, 3144, 4),
            PosRegGroup3 = ParsePoses(data, 3544, 10),
            PosRegGroup4 = ParsePoses(data, 4544, 10),
            PosRegGroup5 = ParsePoses(data, 5544, 10),
            FastClock = RobotBinaryBuffer.ReadInt32LittleEndian(data, 6544),
            Timer10Value = RobotBinaryBuffer.ReadInt32LittleEndian(data, 6548),
            CurrentAngle = RobotBinaryBuffer.ReadSingleLittleEndian(data, 6552),
            DutyTemp = RobotBinaryBuffer.ReadSingleLittleEndian(data, 6556),
            Timer10Comment = RobotBinaryBuffer.ReadNullTrimmedString(data, 6560, 80, encoding),
            Timer2Comment = RobotBinaryBuffer.ReadNullTrimmedString(data, 6640, 80, encoding),
            MenuTool1 = ParsePose(data, 6720),
            HttpKclCommands = RobotBinaryBuffer.ReadNullTrimmedString(data, 6820, 80, encoding),
            NumericRegisters1 = ReadInt32Array(data, 6900, 5),
            NumericRegisters2 = ReadSingleArray(data, 6920, 5),
            DataPosRegMachineGroup = ParseDataPosRegMachineGroup(data)
        };
    }

    private static byte[] ExtractInlinePayload(byte[] response, int expectedByteCount)
    {
        if (expectedByteCount > InlineMaxBytes || InlinePayloadOffset + expectedByteCount > response.Length)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 인라인 응답 크기가 기대 바이트({expectedByteCount})를 담기에 유효하지 않습니다.",
                response);
        }

        byte[] result = new byte[expectedByteCount];
        Array.Copy(response, InlinePayloadOffset, result, 0, expectedByteCount);
        return result;
    }

    private static byte[] ExtractExtendedPayload(byte[] response, int expectedByteCount)
    {
        ushort payloadLength = RobotBinaryBuffer.ReadUInt16LittleEndian(response, 4);
        if (response.Length < HeaderLength + payloadLength || payloadLength < expectedByteCount)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 확장 응답 길이({payloadLength})가 기대 바이트({expectedByteCount})보다 작습니다.",
                response);
        }

        int copyLength = expectedByteCount > 0 ? expectedByteCount : payloadLength;
        byte[] result = new byte[copyLength];
        Array.Copy(response, HeaderLength, result, 0, copyLength);
        return result;
    }

    private static IReadOnlyList<FanucAlarm> ParseAlarms(byte[] data, int offset, int count, Encoding encoding)
    {
        var list = new List<FanucAlarm>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ParseAlarm(data, offset + (i * 200), encoding));
        }

        return list.AsReadOnly();
    }

    private static FanucAlarm ParseAlarm(byte[] data, int offset, Encoding encoding)
    {
        short year = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 10);
        DateTime? time = null;
        if (year > 0)
        {
            try
            {
                time = new DateTime(
                    year,
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 12),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 14),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 16),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 18),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 20));
            }
            catch (ArgumentOutOfRangeException)
            {
                time = null;
            }
        }

        return new FanucAlarm
        {
            AlarmId = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset),
            AlarmNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 2),
            CauseAlarmId = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 4),
            CauseAlarmNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 6),
            Severity = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 8),
            Time = time,
            AlarmMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + 22, 80, encoding),
            CauseAlarmMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + 102, 80, encoding),
            SeverityMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + 182, 18, encoding)
        };
    }

    private static IReadOnlyList<FanucPose> ParsePoses(byte[] data, int offset, int count)
    {
        var list = new List<FanucPose>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ParsePose(data, offset + (i * 100)));
        }

        return list.AsReadOnly();
    }

    private static FanucPose ParsePose(byte[] data, int offset) =>
        new()
        {
            Xyzwpr = ReadSingleArray(data, offset, 9),
            Config = ReadInt16Array(data, offset + 36, 7),
            IsCartesianValid = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 50) != 0,
            Joint = ReadSingleArray(data, offset + 52, 9),
            IsJointValid = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 88) != 0,
            UserFrame = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 90),
            UserTool = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 92)
        };

    private static FanucTask ParseTask(byte[] data, int offset, Encoding encoding) =>
        new()
        {
            ProgramName = RobotBinaryBuffer.ReadNullTrimmedString(data, offset, 16, encoding),
            LineNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 16),
            State = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + 18),
            ParentProgramName = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + 20, 16, encoding)
        };

    private static IReadOnlyList<FanucPose> ParseDataPosRegMachineGroup(byte[] data)
    {
        var list = new List<FanucPose>(10);
        for (int i = 0; i < 10; i++)
        {
            int xyzwprOffset = 7440 + (i * 50);
            int jointOffset = 7940 + (i * 36);
            list.Add(new FanucPose
            {
                Xyzwpr = ReadSingleArray(data, xyzwprOffset, 9),
                Config = ReadInt16Array(data, xyzwprOffset + 36, 7),
                Joint = ReadSingleArray(data, jointOffset, 9)
            });
        }

        return list.AsReadOnly();
    }

    private static int[] ReadInt32Array(byte[] data, int offset, int count)
    {
        int[] values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadInt32LittleEndian(data, offset + (i * 4));
        }

        return values;
    }

    private static short[] ReadInt16Array(byte[] data, int offset, int count)
    {
        short[] values = new short[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + (i * 2));
        }

        return values;
    }

    private static float[] ReadSingleArray(byte[] data, int offset, int count)
    {
        float[] values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadSingleLittleEndian(data, offset + (i * 4));
        }

        return values;
    }
}
