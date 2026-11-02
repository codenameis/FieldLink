namespace FieldLink.PacketBench.Operations;

/// <summary>화면에 보여 줄 한글 이름입니다.</summary>
internal static class OperationText
{
    public static string Describe(ReadInterpretation interpretation) => interpretation switch
    {
        ReadInterpretation.Bit => "비트",
        ReadInterpretation.Word => "워드 (UInt16)",
        ReadInterpretation.Int16 => "INT16 (부호 있음)",
        ReadInterpretation.DWord => "DWORD (UInt32)",
        ReadInterpretation.Int32 => "DINT (Int32)",
        ReadInterpretation.Float32 => "실수 (Float32)",
        ReadInterpretation.Float64 => "배정도 (Float64)",
        ReadInterpretation.RawBytes => "원시 바이트",
        _ => interpretation.ToString(),
    };

    public static string Describe(CountUnit unit) => unit switch
    {
        CountUnit.Word => "워드",
        CountUnit.Byte => "바이트",
        CountUnit.Bit => "비트",
        _ => unit.ToString(),
    };

    public static string Describe(WriteInterpretation interpretation) => interpretation switch
    {
        WriteInterpretation.Bit => "비트",
        WriteInterpretation.Word => "워드 (UInt16)",
        WriteInterpretation.Int16 => "INT16 (부호 있음)",
        WriteInterpretation.DWord => "DWORD (UInt32)",
        WriteInterpretation.Int32 => "DINT (Int32)",
        WriteInterpretation.Float32 => "실수 (Float32)",
        WriteInterpretation.Float64 => "배정도 (Float64)",
        _ => interpretation.ToString(),
    };

    public static string Describe(WriteMode mode) => mode switch
    {
        WriteMode.Block => "블록 쓰기 (한 번에)",
        WriteMode.PerElement => "주소 증가 쓰기 (한 건씩)",
        _ => mode.ToString(),
    };
}
