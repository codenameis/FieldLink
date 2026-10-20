using System;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 통신 옵션입니다.</summary>
public sealed class VigorOptions
{
    /// <summary>PLC 국번(스테이션 번호, 기본 1)입니다.</summary>
    public byte StationNumber { get; set; } = 1;

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
