namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>0x70</c>/<c>0x71</c>) 알람 항목 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class YercAlarmItem
{
    /// <summary>알람 코드입니다.</summary>
    public int AlarmCode { get; init; }

    /// <summary>알람 데이터 영역(12바이트) 원본 복사본입니다.</summary>
    public int AlarmData { get; init; }

    /// <summary>알람 타입 코드입니다.</summary>
    public int AlarmType { get; init; }

    /// <summary>발생 시각 문자열입니다.</summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>알람 메시지 문자열입니다.</summary>
    public string Message { get; init; } = string.Empty;
}
