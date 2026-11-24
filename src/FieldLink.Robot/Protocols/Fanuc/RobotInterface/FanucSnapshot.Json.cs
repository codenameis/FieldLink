using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public sealed partial class FanucSnapshot
{
    /// <summary>스냅샷의 모든 상태·알람·자세·레지스터·코멘트 필드를 JSON 문자열로 직렬화합니다.</summary>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendSummaryFields(builder);
        AppendScalarFields(builder);
        AppendTasksAndAlarms(builder);
        AppendPoseGroups(builder);
        AppendComments(builder);
        builder.Append('}');
        return builder.ToString();
    }

    private void AppendSummaryFields(StringBuilder builder)
    {
        builder.Append("\"TaskProgram\":");
        RobotJsonWriter.AppendQuotedString(builder, Task.ProgramName);
        builder.Append(",\"TaskLine\":");
        builder.Append(Task.LineNumber.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"FastClock\":");
        builder.Append(FastClock.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"UserFrame\":");
        builder.Append(CurrentPose.UserFrame.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"UserTool\":");
        builder.Append(CurrentPose.UserTool.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"CurrentXyzwpr\":");
        RobotJsonWriter.AppendSingleArray(builder, CurrentPose.Xyzwpr);
        builder.Append(",\"CurrentJoint\":");
        RobotJsonWriter.AppendSingleArray(builder, CurrentPose.Joint);
    }

    private void AppendScalarFields(StringBuilder builder)
    {
        builder.Append(",\"Timer10Value\":");
        builder.Append(Timer10Value.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"CurrentAngle\":");
        RobotJsonWriter.AppendSingle(builder, CurrentAngle);
        builder.Append(",\"DutyTemp\":");
        RobotJsonWriter.AppendSingle(builder, DutyTemp);
        builder.Append(",\"Timer10Comment\":");
        RobotJsonWriter.AppendQuotedString(builder, Timer10Comment);
        builder.Append(",\"Timer2Comment\":");
        RobotJsonWriter.AppendQuotedString(builder, Timer2Comment);
        builder.Append(",\"HttpKclCommands\":");
        RobotJsonWriter.AppendQuotedString(builder, HttpKclCommands);
        builder.Append(",\"NumericRegisters1\":");
        RobotJsonWriter.AppendInt32Array(builder, NumericRegisters1);
        builder.Append(",\"NumericRegisters2\":");
        RobotJsonWriter.AppendSingleArray(builder, NumericRegisters2);
    }

    private void AppendTasksAndAlarms(StringBuilder builder)
    {
        builder.Append(",\"AlarmList\":");
        AppendAlarmList(builder, AlarmList);
        builder.Append(",\"AlarmCurrent\":");
        AppendAlarm(builder, AlarmCurrent);
        builder.Append(",\"AlarmPassword\":");
        AppendAlarm(builder, AlarmPassword);
        builder.Append(",\"Task\":");
        AppendTask(builder, Task);
        builder.Append(",\"TaskIgnoreMacro\":");
        AppendTask(builder, TaskIgnoreMacro);
        builder.Append(",\"TaskIgnoreKarel\":");
        AppendTask(builder, TaskIgnoreKarel);
        builder.Append(",\"TaskIgnoreMacroKarel\":");
        AppendTask(builder, TaskIgnoreMacroKarel);
    }

    private void AppendPoseGroups(StringBuilder builder)
    {
        builder.Append(",\"CurrentPose\":");
        AppendPose(builder, CurrentPose);
        builder.Append(",\"CurrentPoseUserFrame\":");
        AppendPose(builder, CurrentPoseUserFrame);
        builder.Append(",\"GroupCurrentPoses\":");
        AppendPoseList(builder, GroupCurrentPoses);
        builder.Append(",\"PosRegGroup1\":");
        AppendPoseList(builder, PosRegGroup1);
        builder.Append(",\"PosRegGroup2\":");
        AppendPoseList(builder, PosRegGroup2);
        builder.Append(",\"PosRegGroup3\":");
        AppendPoseList(builder, PosRegGroup3);
        builder.Append(",\"PosRegGroup4\":");
        AppendPoseList(builder, PosRegGroup4);
        builder.Append(",\"PosRegGroup5\":");
        AppendPoseList(builder, PosRegGroup5);
        builder.Append(",\"MenuTool1\":");
        AppendPose(builder, MenuTool1);
        builder.Append(",\"DataPosRegMachineGroup\":");
        AppendPoseList(builder, DataPosRegMachineGroup);
    }

    private void AppendComments(StringBuilder builder)
    {
        AppendStringListProperty(builder, "DigitalInputComments", DigitalInputComments);
        AppendStringListProperty(builder, "DigitalOutputComments", DigitalOutputComments);
        AppendStringListProperty(builder, "RobotInputComments", RobotInputComments);
        AppendStringListProperty(builder, "RobotOutputComments", RobotOutputComments);
        AppendStringListProperty(builder, "UserInputComments", UserInputComments);
        AppendStringListProperty(builder, "UserOutputComments", UserOutputComments);
        AppendStringListProperty(builder, "SopInputComments", SopInputComments);
        AppendStringListProperty(builder, "SopOutputComments", SopOutputComments);
        AppendStringListProperty(builder, "WeldInputComments", WeldInputComments);
        AppendStringListProperty(builder, "WeldOutputComments", WeldOutputComments);
        AppendStringListProperty(builder, "WeldStickInputComments", WeldStickInputComments);
        AppendStringListProperty(builder, "AnalogInputComments", AnalogInputComments);
        AppendStringListProperty(builder, "AnalogOutputComments", AnalogOutputComments);
        AppendStringListProperty(builder, "GroupInputComments", GroupInputComments);
        AppendStringListProperty(builder, "GroupOutputComments", GroupOutputComments);
        AppendStringListProperty(builder, "StringRegisters", StringRegisters);
        AppendStringListProperty(builder, "StringRegisterComments", StringRegisterComments);
    }

    private static void AppendAlarmList(StringBuilder builder, IReadOnlyList<FanucAlarm> alarms)
    {
        builder.Append('[');
        for (int i = 0; i < alarms.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendAlarm(builder, alarms[i]);
        }

        builder.Append(']');
    }

    private static void AppendAlarm(StringBuilder builder, FanucAlarm alarm)
    {
        builder.Append("{\"AlarmId\":");
        builder.Append(alarm.AlarmId.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"AlarmNumber\":");
        builder.Append(alarm.AlarmNumber.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"CauseAlarmId\":");
        builder.Append(alarm.CauseAlarmId.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"CauseAlarmNumber\":");
        builder.Append(alarm.CauseAlarmNumber.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"Severity\":");
        builder.Append(alarm.Severity.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"Time\":");
        RobotJsonWriter.AppendQuotedString(builder, alarm.Time?.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(",\"AlarmMessage\":");
        RobotJsonWriter.AppendQuotedString(builder, alarm.AlarmMessage);
        builder.Append(",\"CauseAlarmMessage\":");
        RobotJsonWriter.AppendQuotedString(builder, alarm.CauseAlarmMessage);
        builder.Append(",\"SeverityMessage\":");
        RobotJsonWriter.AppendQuotedString(builder, alarm.SeverityMessage);
        builder.Append('}');
    }

    private static void AppendTask(StringBuilder builder, FanucTask task)
    {
        builder.Append("{\"ProgramName\":");
        RobotJsonWriter.AppendQuotedString(builder, task.ProgramName);
        builder.Append(",\"LineNumber\":");
        builder.Append(task.LineNumber.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"State\":");
        builder.Append(task.State.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"ParentProgramName\":");
        RobotJsonWriter.AppendQuotedString(builder, task.ParentProgramName);
        builder.Append('}');
    }

    private static void AppendPoseList(StringBuilder builder, IReadOnlyList<FanucPose> poses)
    {
        builder.Append('[');
        for (int i = 0; i < poses.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendPose(builder, poses[i]);
        }

        builder.Append(']');
    }

    private static void AppendPose(StringBuilder builder, FanucPose pose)
    {
        builder.Append("{\"Xyzwpr\":");
        RobotJsonWriter.AppendSingleArray(builder, pose.Xyzwpr);
        builder.Append(",\"Config\":");
        AppendInt16Array(builder, pose.Config);
        builder.Append(",\"IsCartesianValid\":");
        builder.Append(pose.IsCartesianValid ? "true" : "false");
        builder.Append(",\"Joint\":");
        RobotJsonWriter.AppendSingleArray(builder, pose.Joint);
        builder.Append(",\"IsJointValid\":");
        builder.Append(pose.IsJointValid ? "true" : "false");
        builder.Append(",\"UserFrame\":");
        builder.Append(pose.UserFrame.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"UserTool\":");
        builder.Append(pose.UserTool.ToString(CultureInfo.InvariantCulture));
        builder.Append('}');
    }

    private static void AppendInt16Array(StringBuilder builder, IReadOnlyList<short> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    private static void AppendStringListProperty(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append(",\"");
        builder.Append(name);
        builder.Append("\":[");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            RobotJsonWriter.AppendQuotedString(builder, values[i]);
        }

        builder.Append(']');
    }
}
