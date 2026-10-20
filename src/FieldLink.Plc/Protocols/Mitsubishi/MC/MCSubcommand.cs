namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 서브커맨드 코드입니다. 시리즈와 접근 단위(워드·비트)로 정해집니다. (사양서 §3-2)</summary>
public enum MCSubcommand : ushort
{
    /// <summary>QnA · Q · L · iQ-L 시리즈의 워드 단위 접근.</summary>
    QLWord = 0x0000,

    /// <summary>QnA · Q · L · iQ-L 시리즈의 비트 단위 접근.</summary>
    QLBit = 0x0001,

    /// <summary>iQ-R 시리즈의 워드 단위 접근.</summary>
    IqrWord = 0x0002,

    /// <summary>iQ-R 시리즈의 비트 단위 접근.</summary>
    IqrBit = 0x0003,
}
