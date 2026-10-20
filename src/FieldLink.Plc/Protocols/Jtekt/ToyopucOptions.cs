namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 통신 옵션입니다.</summary>
public sealed class ToyopucOptions
{
    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 500입니다.</summary>
    public int MaxReadWords { get; init; } = 500;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 500입니다.</summary>
    public int MaxWriteWords { get; init; } = 500;
}
