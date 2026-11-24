namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>0x89</c>) 컨트롤러 시스템 정보 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class YercSystemInfo
{
    /// <summary>소프트웨어 버전 문자열입니다.</summary>
    public string SoftwareVersion { get; init; } = string.Empty;

    /// <summary>로봇/애플리케이션 기종명입니다.</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>파라미터 버전 문자열입니다.</summary>
    public string ParameterVersion { get; init; } = string.Empty;
}
