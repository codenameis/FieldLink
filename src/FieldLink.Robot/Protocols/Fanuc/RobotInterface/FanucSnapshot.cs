using System;
using System.Collections.Generic;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface 전체 스냅샷(<c>D1</c> 기준 6130워드) 디코딩 모델입니다.</summary>
public sealed partial class FanucSnapshot
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

    /// <summary>그룹 3 위치 레지스터(10개)입니다.</summary>
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

    /// <summary>DI 코멘트 배열(<c>DI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> DigitalInputComments { get; init; } = Array.Empty<string>();

    /// <summary>DO 코멘트 배열(<c>DO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> DigitalOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>RI 코멘트 배열(<c>RI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> RobotInputComments { get; init; } = Array.Empty<string>();

    /// <summary>RO 코멘트 배열(<c>RO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> RobotOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>UI 코멘트 배열(<c>UI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> UserInputComments { get; init; } = Array.Empty<string>();

    /// <summary>UO 코멘트 배열(<c>UO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> UserOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>SI 코멘트 배열(<c>SI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> SopInputComments { get; init; } = Array.Empty<string>();

    /// <summary>SO 코멘트 배열(<c>SO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> SopOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>WI 코멘트 배열(<c>WI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> WeldInputComments { get; init; } = Array.Empty<string>();

    /// <summary>WO 코멘트 배열(<c>WO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> WeldOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>WSI 코멘트 배열(<c>WSI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> WeldStickInputComments { get; init; } = Array.Empty<string>();

    /// <summary>AI 코멘트 배열(<c>AI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> AnalogInputComments { get; init; } = Array.Empty<string>();

    /// <summary>AO 코멘트 배열(<c>AO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> AnalogOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>GI 코멘트 배열(<c>GI[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> GroupInputComments { get; init; } = Array.Empty<string>();

    /// <summary>GO 코멘트 배열(<c>GO[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> GroupOutputComments { get; init; } = Array.Empty<string>();

    /// <summary>문자열 레지스터 배열(<c>SR[1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> StringRegisters { get; init; } = Array.Empty<string>();

    /// <summary>문자열 레지스터 코멘트 배열(<c>SR[C1]</c>, 3개)입니다.</summary>
    public IReadOnlyList<string> StringRegisterComments { get; init; } = Array.Empty<string>();
}
