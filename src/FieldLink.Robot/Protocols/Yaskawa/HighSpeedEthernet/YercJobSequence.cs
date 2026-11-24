namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>0x73</c>) 실행 JOB 정보 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class YercJobSequence
{
    /// <summary>현재 실행 중인 JOB 이름입니다.</summary>
    public string JobName { get; init; } = string.Empty;

    /// <summary>현재 실행 라인 번호입니다.</summary>
    public int LineNumber { get; init; }

    /// <summary>현재 스텝 번호입니다.</summary>
    public int StepNumber { get; init; }

    /// <summary>속도 오버라이드 비율 값입니다.</summary>
    public int SpeedOverride { get; init; }
}
