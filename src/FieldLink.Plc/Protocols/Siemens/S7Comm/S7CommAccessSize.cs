namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>주소 표기의 크기 문자(X, B, W, D)가 뜻하는 접근 단위입니다. (사양서 §2)</summary>
public enum S7CommAccessSize
{
    /// <summary>비트 (X 또는 크기 문자 없음).</summary>
    Bit,

    /// <summary>바이트 (B).</summary>
    Byte,

    /// <summary>워드 (W).</summary>
    Word,

    /// <summary>더블워드 (D).</summary>
    DoubleWord,
}
