namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 통신 프로토콜 옵션입니다.</summary>
public sealed class GESrtpOptions
{
    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxReadBytes { get; set; } = 1024;

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxWriteBytes { get; set; } = 1024;
}
