namespace FieldLink.Plc.Protocols.Megmeet;

/// <summary>Megmeet PLC 통신 옵션입니다.</summary>
public sealed class MegmeetOptions
{
    /// <summary>슬레이브 국번(Station 번호)입니다. 기본값은 1입니다.</summary>
    public byte Station { get; init; } = 1;

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxReadWords { get; init; } = 120;

    /// <summary>1회 요청당 최대 읽기 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxReadBits { get; init; } = 1920;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxWriteWords { get; init; } = 120;

    /// <summary>1회 요청당 최대 쓰기 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxWriteBits { get; init; } = 1920;
}
