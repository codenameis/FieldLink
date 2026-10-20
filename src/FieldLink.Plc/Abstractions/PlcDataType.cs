namespace FieldLink.Plc.Abstractions;

/// <summary>PLC에 읽고 쓰는 값 한 개(원소)의 자료형입니다.</summary>
/// <remarks>이름은 .NET 기본 형식 이름을 따릅니다. 괄호 안은 IEC 61131-3 자료형입니다.</remarks>
public enum PlcDataType
{
    /// <summary>비트 하나 (BOOL). 요청·응답 바이트에서는 원소 하나를 0 또는 1인 바이트 하나로 표현합니다.</summary>
    Boolean,

    /// <summary>부호 없는 8비트 (BYTE, USINT).</summary>
    Byte,

    /// <summary>부호 있는 16비트 (INT).</summary>
    Int16,

    /// <summary>부호 없는 16비트 (WORD, UINT).</summary>
    UInt16,

    /// <summary>부호 있는 32비트 (DINT).</summary>
    Int32,

    /// <summary>부호 없는 32비트 (DWORD, UDINT).</summary>
    UInt32,

    /// <summary>부호 있는 64비트 (LINT).</summary>
    Int64,

    /// <summary>부호 없는 64비트 (LWORD, ULINT).</summary>
    UInt64,

    /// <summary>32비트 부동소수점 (REAL, C# float).</summary>
    Single,

    /// <summary>64비트 부동소수점 (LREAL, C# double).</summary>
    Double,

    /// <summary>문자열 (STRING). 원소 하나는 저장 바이트 하나입니다.</summary>
    String,
}
