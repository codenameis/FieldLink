namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 통신 옵션입니다.</summary>
public sealed class MewtocolOptions
{
    /// <summary>PLC 슬레이브 국번(1~99, 또는 1:1 직결 국번 0xEE)입니다. 기본값은 0xEE입니다. (사양서 §1.1)</summary>
    public byte Station { get; init; } = 0xEE;

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 500입니다. (사양서 §1.6)</summary>
    public int MaxReadWords { get; init; } = 500;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 500입니다.</summary>
    public int MaxWriteWords { get; init; } = 500;
}
