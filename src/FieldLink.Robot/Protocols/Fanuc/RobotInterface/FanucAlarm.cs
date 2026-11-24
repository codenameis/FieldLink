using System;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC 컨트롤러 알람 항목(200바이트) 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class FanucAlarm
{
    /// <summary>알람 ID입니다.</summary>
    public short AlarmId { get; init; }

    /// <summary>알람 번호입니다.</summary>
    public short AlarmNumber { get; init; }

    /// <summary>원인 알람 ID입니다.</summary>
    public short CauseAlarmId { get; init; }

    /// <summary>원인 알람 번호입니다.</summary>
    public short CauseAlarmNumber { get; init; }

    /// <summary>심각도 코드입니다.</summary>
    public short Severity { get; init; }

    /// <summary>알람 발생 시각입니다. 연도가 0 이하이면 null입니다.</summary>
    public DateTime? Time { get; init; }

    /// <summary>알람 메시지 문자열입니다.</summary>
    public string AlarmMessage { get; init; } = string.Empty;

    /// <summary>원인 알람 메시지 문자열입니다.</summary>
    public string CauseAlarmMessage { get; init; } = string.Empty;

    /// <summary>심각도 설명 문자열입니다.</summary>
    public string SeverityMessage { get; init; } = string.Empty;
}
