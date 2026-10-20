namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 통신 옵션입니다.</summary>
public sealed class FatekOptions
{
    /// <summary>PLC 슬레이브 국번(Station 번호)입니다. 기본값은 1입니다. (1~254)</summary>
    public byte Station { get; init; } = 1;

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxReadWords { get; init; } = 64;

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxReadBits { get; init; } = 255;

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxWriteWords { get; init; } = 64;

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxWriteBits { get; init; } = 255;
}
