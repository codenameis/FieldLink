using System;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 통신 옵션입니다.</summary>
public sealed class XinjeOptions
{
    /// <summary>대상 PLC 시리즈입니다. 기본값은 <see cref="XinjeSeries.XdXl"/>입니다.</summary>
    public XinjeSeries Series { get; set; } = XinjeSeries.XdXl;

    /// <summary>확장 Modbus 기능 코드(0x1E~0x21) 사용 여부입니다. 기본값은 true입니다.</summary>
    public bool UseExtendedProtocol { get; set; } = true;

    /// <summary>슬레이브 PLC 국번입니다. 기본값은 1입니다.</summary>
    public byte Station { get; set; } = 1;

    /// <summary>통신 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 120입니다. (매뉴얼 §A.1.5)</summary>
    public int MaxReadWords { get; set; } = 120;

    /// <summary>1회 요청당 최대 읽기 비트 수입니다. 기본값은 1920입니다. (매뉴얼 §A.1.5)</summary>
    public int MaxReadBits { get; set; } = 1920;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxWriteWords { get; set; } = 120;

    /// <summary>1회 요청당 최대 쓰기 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxWriteBits { get; set; } = 1920;
}
