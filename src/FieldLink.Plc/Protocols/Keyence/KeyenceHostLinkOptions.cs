namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 통신 옵션입니다.</summary>
public sealed class KeyenceHostLinkOptions
{
    /// <summary>멀티드롭 국번(0~99)입니다. null이면 1:1 직결(국번 생략) 모드입니다.</summary>
    public byte? Station { get; init; }

    /// <summary>1회 요청당 최대 읽기 요소 수입니다. 기본값은 256입니다. (사양서 §A.1.3)</summary>
    public int MaxReadElements { get; init; } = 256;

    /// <summary>1회 요청당 최대 쓰기 요소 수입니다. 기본값은 256입니다.</summary>
    public int MaxWriteElements { get; init; } = 256;
}
