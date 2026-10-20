namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 통신 프로토콜 옵션입니다.</summary>
public sealed class FujiElectricSpbOptions
{
    /// <summary>PLC 슬레이브 국번(1~255)입니다. 기본값은 1입니다.</summary>
    public byte Station { get; set; } = 1;

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 64입니다. (사양서 §A.1.4)</summary>
    public int MaxReadWords { get; set; } = 64;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 64입니다.</summary>
    public int MaxWriteWords { get; set; } = 64;
}
