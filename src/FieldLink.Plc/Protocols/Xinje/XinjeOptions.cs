using System;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 통신 옵션입니다.</summary>
public sealed class XinjeOptions
{
    /// <summary>PLC 시리즈(기본 XdXl)입니다.</summary>
    public XinjeSeries Series { get; set; } = XinjeSeries.XdXl;

    /// <summary>확장 Modbus 기능 코드(0x1E~0x21) 사용 여부(기본 true)입니다.</summary>
    public bool UseExtendedProtocol { get; set; } = true;

    /// <summary>PLC 국번(기본 1)입니다.</summary>
    public byte Station { get; set; } = 1;

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
