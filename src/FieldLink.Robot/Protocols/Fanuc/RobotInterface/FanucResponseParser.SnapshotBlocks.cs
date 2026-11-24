using System;
using System.Collections.Generic;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public static partial class FanucResponseParser
{
    private const int AlarmEntryByteLength = 200;
    private const int AlarmNumberOffset = 2;
    private const int CauseAlarmIdOffset = 4;
    private const int CauseAlarmNumberOffset = 6;
    private const int AlarmSeverityOffset = 8;
    private const int AlarmYearOffset = 10;
    private const int AlarmMonthOffset = 12;
    private const int AlarmDayOffset = 14;
    private const int AlarmHourOffset = 16;
    private const int AlarmMinuteOffset = 18;
    private const int AlarmSecondOffset = 20;
    private const int AlarmMessageOffset = 22;
    private const int CauseAlarmMessageOffset = 102;
    private const int AlarmSeverityMessageOffset = 182;
    private const int AlarmSeverityMessageLength = 18;

    private const int PoseEntryByteLength = 100;
    private const int PoseXyzwprCount = 9;
    private const int PoseConfigOffset = 36;
    private const int PoseConfigCount = 7;
    private const int PoseCartesianValidOffset = 50;
    private const int PoseJointOffset = 52;
    private const int PoseJointCount = 9;
    private const int PoseJointValidOffset = 88;
    private const int PoseUserFrameOffset = 90;
    private const int PoseUserToolOffset = 92;

    private const int TaskProgramNameLength = 16;
    private const int TaskLineNumberOffset = 16;
    private const int TaskStateOffset = 18;
    private const int TaskParentProgramOffset = 20;

    private const int DataPosRegMachineGroupXyzwprOffset = 7440;
    private const int DataPosRegMachineGroupXyzwprStride = 50;
    private const int DataPosRegMachineGroupJointOffset = 7940;
    private const int DataPosRegMachineGroupJointStride = 36;
    private const int DataPosRegMachineGroupCount = 10;

    private const int StringEntryByteLength = 80;
    private const int Int16ByteSize = sizeof(short);
    private const int Int32ByteSize = sizeof(int);
    private const int SingleByteSize = sizeof(float);

    private static IReadOnlyList<FanucAlarm> ParseAlarms(byte[] data, int offset, int count, Encoding encoding)
    {
        var list = new List<FanucAlarm>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ParseAlarm(data, offset + (i * AlarmEntryByteLength), encoding));
        }

        return list.AsReadOnly();
    }

    private static FanucAlarm ParseAlarm(byte[] data, int offset, Encoding encoding)
    {
        short year = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmYearOffset);
        DateTime? time = null;
        if (year > 0)
        {
            try
            {
                time = new DateTime(
                    year,
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmMonthOffset),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmDayOffset),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmHourOffset),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmMinuteOffset),
                    RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmSecondOffset));
            }
            catch (ArgumentOutOfRangeException)
            {
                time = null;
            }
        }

        return new FanucAlarm
        {
            AlarmId = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset),
            AlarmNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmNumberOffset),
            CauseAlarmId = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + CauseAlarmIdOffset),
            CauseAlarmNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + CauseAlarmNumberOffset),
            Severity = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + AlarmSeverityOffset),
            Time = time,
            AlarmMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + AlarmMessageOffset, StringEntryByteLength, encoding),
            CauseAlarmMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + CauseAlarmMessageOffset, StringEntryByteLength, encoding),
            SeverityMessage = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + AlarmSeverityMessageOffset, AlarmSeverityMessageLength, encoding)
        };
    }

    private static IReadOnlyList<FanucPose> ParsePoses(byte[] data, int offset, int count)
    {
        var list = new List<FanucPose>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(ParsePose(data, offset + (i * PoseEntryByteLength)));
        }

        return list.AsReadOnly();
    }

    private static FanucPose ParsePose(byte[] data, int offset) =>
        new()
        {
            Xyzwpr = ReadSingleArray(data, offset, PoseXyzwprCount),
            Config = ReadInt16Array(data, offset + PoseConfigOffset, PoseConfigCount),
            IsCartesianValid = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + PoseCartesianValidOffset) != 0,
            Joint = ReadSingleArray(data, offset + PoseJointOffset, PoseJointCount),
            IsJointValid = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + PoseJointValidOffset) != 0,
            UserFrame = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + PoseUserFrameOffset),
            UserTool = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + PoseUserToolOffset)
        };

    private static FanucTask ParseTask(byte[] data, int offset, Encoding encoding) =>
        new()
        {
            ProgramName = RobotBinaryBuffer.ReadNullTrimmedString(data, offset, TaskProgramNameLength, encoding),
            LineNumber = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + TaskLineNumberOffset),
            State = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + TaskStateOffset),
            ParentProgramName = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + TaskParentProgramOffset, TaskProgramNameLength, encoding)
        };

    private static IReadOnlyList<FanucPose> ParseDataPosRegMachineGroup(byte[] data)
    {
        var list = new List<FanucPose>(DataPosRegMachineGroupCount);
        for (int i = 0; i < DataPosRegMachineGroupCount; i++)
        {
            int xyzwprOffset = DataPosRegMachineGroupXyzwprOffset + (i * DataPosRegMachineGroupXyzwprStride);
            int jointOffset = DataPosRegMachineGroupJointOffset + (i * DataPosRegMachineGroupJointStride);
            list.Add(new FanucPose
            {
                Xyzwpr = ReadSingleArray(data, xyzwprOffset, PoseXyzwprCount),
                Config = ReadInt16Array(data, xyzwprOffset + PoseConfigOffset, PoseConfigCount),
                Joint = ReadSingleArray(data, jointOffset, PoseJointCount)
            });
        }

        return list.AsReadOnly();
    }

    private static IReadOnlyList<string> ParseStringArray(byte[] data, int offset, int count, Encoding encoding)
    {
        string[] values = new string[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadNullTrimmedString(data, offset + (i * StringEntryByteLength), StringEntryByteLength, encoding);
        }

        return Array.AsReadOnly(values);
    }

    private static int[] ReadInt32Array(byte[] data, int offset, int count)
    {
        int[] values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadInt32LittleEndian(data, offset + (i * Int32ByteSize));
        }

        return values;
    }

    private static short[] ReadInt16Array(byte[] data, int offset, int count)
    {
        short[] values = new short[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadInt16LittleEndian(data, offset + (i * Int16ByteSize));
        }

        return values;
    }

    private static float[] ReadSingleArray(byte[] data, int offset, int count)
    {
        float[] values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadSingleLittleEndian(data, offset + (i * SingleByteSize));
        }

        return values;
    }
}
