namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta PLC 통신 옵션입니다.</summary>
public sealed class DeltaOptions
{
    /// <summary>대상 Delta PLC 시리즈입니다. 기본값은 <see cref="DeltaSeries.Dvp"/>입니다.</summary>
    public DeltaSeries Series { get; init; } = DeltaSeries.Dvp;

    /// <summary>슬레이브 국번(Station 번호)입니다. 기본값은 1입니다.</summary>
    public byte Station { get; init; } = 1;

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxReadWords { get; init; } = 120;

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxReadBits { get; init; } = 1920;

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxWriteWords { get; init; } = 120;

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxWriteBits { get; init; } = 1920;
}
