using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface 전체 스냅샷(<c>D1</c> 기준 6130워드) 디코딩 모델입니다.</summary>
public sealed class FanucSnapshot
{
    /// <summary>활성 알람 목록(최대 5개)입니다.</summary>
    public IReadOnlyList<FanucAlarm> AlarmList { get; init; } = Array.Empty<FanucAlarm>();

    /// <summary>현재 알람입니다.</summary>
    public FanucAlarm AlarmCurrent { get; init; } = new();

    /// <summary>패스워드 알람입니다.</summary>
    public FanucAlarm AlarmPassword { get; init; } = new();

    /// <summary>현재 자세입니다.</summary>
    public FanucPose CurrentPose { get; init; } = new();

    /// <summary>사용자 좌표계 기준 현재 자세입니다.</summary>
    public FanucPose CurrentPoseUserFrame { get; init; } = new();

    /// <summary>그룹 2~5 현재 자세 목록입니다.</summary>
    public IReadOnlyList<FanucPose> GroupCurrentPoses { get; init; } = Array.Empty<FanucPose>();

    /// <summary>현재 실행 태스크입니다.</summary>
    public FanucTask Task { get; init; } = new();

    /// <summary>매크로 제외 태스크입니다.</summary>
    public FanucTask TaskIgnoreMacro { get; init; } = new();

    /// <summary>KAREL 제외 태스크입니다.</summary>
    public FanucTask TaskIgnoreKarel { get; init; } = new();

    /// <summary>매크로 및 KAREL 제외 태스크입니다.</summary>
    public FanucTask TaskIgnoreMacroKarel { get; init; } = new();

    /// <summary>그룹 1 위치 레지스터(10개)입니다.</summary>
    public IReadOnlyList<FanucPose> PosRegGroup1 { get; init; } = Array.Empty<FanucPose>();

    /// <summary>그룹 2 위치 레지스터(4개)입니다.</summary>
    public IReadOnlyList<FanucPose> PosRegGroup2 { get; init; } = Array.Empty<FanucPose>();

    /// <summary>그룹 3~5 위치 레지스터입니다.</summary>
    public IReadOnlyList<FanucPose> PosRegGroup3 { get; init; } = Array.Empty<FanucPose>();

    /// <summary>그룹 4 위치 레지스터(10개)입니다.</summary>
    public IReadOnlyList<FanucPose> PosRegGroup4 { get; init; } = Array.Empty<FanucPose>();

    /// <summary>그룹 5 위치 레지스터(10개)입니다.</summary>
    public IReadOnlyList<FanucPose> PosRegGroup5 { get; init; } = Array.Empty<FanucPose>();

    /// <summary><c>$FAST_CLOCK</c> 정수 값입니다.</summary>
    public int FastClock { get; init; }

    /// <summary><c>$TIMER[10].$TIMER_VAL</c> 정수 값입니다.</summary>
    public int Timer10Value { get; init; }

    /// <summary><c>$MOR_GRP[1].$CURRENT_ANG[1]</c> 각도입니다.</summary>
    public float CurrentAngle { get; init; }

    /// <summary><c>$DUTY_TEMP</c> 온도 값입니다.</summary>
    public float DutyTemp { get; init; }

    /// <summary>타이머 10 코멘트 문자열입니다.</summary>
    public string Timer10Comment { get; init; } = string.Empty;

    /// <summary>타이머 2 코멘트 문자열입니다.</summary>
    public string Timer2Comment { get; init; } = string.Empty;

    /// <summary><c>$MNUTOOL[1,1]</c> 자세입니다.</summary>
    public FanucPose MenuTool1 { get; init; } = new();

    /// <summary><c>$[HTTPKCL]CMDS[1]</c> 문자열입니다.</summary>
    public string HttpKclCommands { get; init; } = string.Empty;

    /// <summary>정수형 수치 레지스터 R1~R5입니다.</summary>
    public IReadOnlyList<int> NumericRegisters1 { get; init; } = Array.Empty<int>();

    /// <summary>실수형 수치 레지스터 R6~R10입니다.</summary>
    public IReadOnlyList<float> NumericRegisters2 { get; init; } = Array.Empty<float>();

    /// <summary>분리 영역에서 조합된 <c>DataPosRegMG[10]</c> 목록입니다.</summary>
    public IReadOnlyList<FanucPose> DataPosRegMachineGroup { get; init; } = Array.Empty<FanucPose>();

    /// <summary>스냅샷 주요 요약 정보를 JSON 문자열로 직렬화합니다.</summary>
    public string ToJson()
    {
        var builder = new StringBuilder();
        builder.Append("{\"TaskProgram\":");
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
        builder.Append('}');
        return builder.ToString();
    }
}
