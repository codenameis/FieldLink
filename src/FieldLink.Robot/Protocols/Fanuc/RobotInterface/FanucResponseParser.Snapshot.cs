using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public static partial class FanucResponseParser
{
    private const int RequiredSnapshotByteLength = 12260;
    private const int AlarmListOffset = 0;
    private const int AlarmListCount = 5;
    private const int AlarmCurrentOffset = 1000;
    private const int AlarmPasswordOffset = 1200;

    private const int CurrentPoseOffset = 1400;
    private const int CurrentPoseUserFrameOffset = 1500;
    private const int GroupCurrentPosesOffset = 1600;
    private const int GroupCurrentPosesCount = 4;

    private const int TaskOffset = 2000;
    private const int TaskIgnoreMacroOffset = 2036;
    private const int TaskIgnoreKarelOffset = 2072;
    private const int TaskIgnoreMacroKarelOffset = 2108;

    private const int PosRegGroup1Offset = 2144;
    private const int PosRegGroup1Count = 10;
    private const int PosRegGroup2Offset = 3144;
    private const int PosRegGroup2Count = 4;
    private const int PosRegGroup3Offset = 3544;
    private const int PosRegGroup4Offset = 4544;
    private const int PosRegGroup5Offset = 5544;
    private const int PosRegStandardCount = 10;

    private const int FastClockOffset = 6544;
    private const int Timer10ValueOffset = 6548;
    private const int CurrentAngleOffset = 6552;
    private const int DutyTempOffset = 6556;
    private const int Timer10CommentOffset = 6560;
    private const int Timer2CommentOffset = 6640;
    private const int MenuTool1Offset = 6720;
    private const int HttpKclCommandsOffset = 6820;
    private const int NumericRegisters1Offset = 6900;
    private const int NumericRegisters2Offset = 6920;
    private const int NumericRegisterBlockCount = 5;

    private const int CommentArrayEntryCount = 3;
    private const int DigitalInputCommentsOffset = 8180;
    private const int DigitalOutputCommentsOffset = 8420;
    private const int RobotInputCommentsOffset = 8660;
    private const int RobotOutputCommentsOffset = 8900;
    private const int UserInputCommentsOffset = 9140;
    private const int UserOutputCommentsOffset = 9380;
    private const int SopInputCommentsOffset = 9620;
    private const int SopOutputCommentsOffset = 9860;
    private const int WeldInputCommentsOffset = 10100;
    private const int WeldOutputCommentsOffset = 10340;
    private const int WeldStickInputCommentsOffset = 10580;
    private const int AnalogInputCommentsOffset = 10820;
    private const int AnalogOutputCommentsOffset = 11060;
    private const int GroupInputCommentsOffset = 11300;
    private const int GroupOutputCommentsOffset = 11540;
    private const int StringRegistersOffset = 11780;
    private const int StringRegisterCommentsOffset = 12020;

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
                $"FANUC 스냅샷 데이터 길이가 {RequiredSnapshotByteLength}바이트보다 짧습니다. (실제: {data.Length} 바이트)",
                data);
        }

        return new FanucSnapshot
        {
            AlarmList = ParseAlarms(data, AlarmListOffset, AlarmListCount, encoding),
            AlarmCurrent = ParseAlarm(data, AlarmCurrentOffset, encoding),
            AlarmPassword = ParseAlarm(data, AlarmPasswordOffset, encoding),
            CurrentPose = ParsePose(data, CurrentPoseOffset),
            CurrentPoseUserFrame = ParsePose(data, CurrentPoseUserFrameOffset),
            GroupCurrentPoses = ParsePoses(data, GroupCurrentPosesOffset, GroupCurrentPosesCount),
            Task = ParseTask(data, TaskOffset, encoding),
            TaskIgnoreMacro = ParseTask(data, TaskIgnoreMacroOffset, encoding),
            TaskIgnoreKarel = ParseTask(data, TaskIgnoreKarelOffset, encoding),
            TaskIgnoreMacroKarel = ParseTask(data, TaskIgnoreMacroKarelOffset, encoding),
            PosRegGroup1 = ParsePoses(data, PosRegGroup1Offset, PosRegGroup1Count),
            PosRegGroup2 = ParsePoses(data, PosRegGroup2Offset, PosRegGroup2Count),
            PosRegGroup3 = ParsePoses(data, PosRegGroup3Offset, PosRegStandardCount),
            PosRegGroup4 = ParsePoses(data, PosRegGroup4Offset, PosRegStandardCount),
            PosRegGroup5 = ParsePoses(data, PosRegGroup5Offset, PosRegStandardCount),
            FastClock = RobotBinaryBuffer.ReadInt32LittleEndian(data, FastClockOffset),
            Timer10Value = RobotBinaryBuffer.ReadInt32LittleEndian(data, Timer10ValueOffset),
            CurrentAngle = RobotBinaryBuffer.ReadSingleLittleEndian(data, CurrentAngleOffset),
            DutyTemp = RobotBinaryBuffer.ReadSingleLittleEndian(data, DutyTempOffset),
            Timer10Comment = RobotBinaryBuffer.ReadNullTrimmedString(data, Timer10CommentOffset, StringEntryByteLength, encoding),
            Timer2Comment = RobotBinaryBuffer.ReadNullTrimmedString(data, Timer2CommentOffset, StringEntryByteLength, encoding),
            MenuTool1 = ParsePose(data, MenuTool1Offset),
            HttpKclCommands = RobotBinaryBuffer.ReadNullTrimmedString(data, HttpKclCommandsOffset, StringEntryByteLength, encoding),
            NumericRegisters1 = ReadInt32Array(data, NumericRegisters1Offset, NumericRegisterBlockCount),
            NumericRegisters2 = ReadSingleArray(data, NumericRegisters2Offset, NumericRegisterBlockCount),
            DataPosRegMachineGroup = ParseDataPosRegMachineGroup(data),
            DigitalInputComments = ParseStringArray(data, DigitalInputCommentsOffset, CommentArrayEntryCount, encoding),
            DigitalOutputComments = ParseStringArray(data, DigitalOutputCommentsOffset, CommentArrayEntryCount, encoding),
            RobotInputComments = ParseStringArray(data, RobotInputCommentsOffset, CommentArrayEntryCount, encoding),
            RobotOutputComments = ParseStringArray(data, RobotOutputCommentsOffset, CommentArrayEntryCount, encoding),
            UserInputComments = ParseStringArray(data, UserInputCommentsOffset, CommentArrayEntryCount, encoding),
            UserOutputComments = ParseStringArray(data, UserOutputCommentsOffset, CommentArrayEntryCount, encoding),
            SopInputComments = ParseStringArray(data, SopInputCommentsOffset, CommentArrayEntryCount, encoding),
            SopOutputComments = ParseStringArray(data, SopOutputCommentsOffset, CommentArrayEntryCount, encoding),
            WeldInputComments = ParseStringArray(data, WeldInputCommentsOffset, CommentArrayEntryCount, encoding),
            WeldOutputComments = ParseStringArray(data, WeldOutputCommentsOffset, CommentArrayEntryCount, encoding),
            WeldStickInputComments = ParseStringArray(data, WeldStickInputCommentsOffset, CommentArrayEntryCount, encoding),
            AnalogInputComments = ParseStringArray(data, AnalogInputCommentsOffset, CommentArrayEntryCount, encoding),
            AnalogOutputComments = ParseStringArray(data, AnalogOutputCommentsOffset, CommentArrayEntryCount, encoding),
            GroupInputComments = ParseStringArray(data, GroupInputCommentsOffset, CommentArrayEntryCount, encoding),
            GroupOutputComments = ParseStringArray(data, GroupOutputCommentsOffset, CommentArrayEntryCount, encoding),
            StringRegisters = ParseStringArray(data, StringRegistersOffset, CommentArrayEntryCount, encoding),
            StringRegisterComments = ParseStringArray(data, StringRegisterCommentsOffset, CommentArrayEntryCount, encoding)
        };
    }
}
