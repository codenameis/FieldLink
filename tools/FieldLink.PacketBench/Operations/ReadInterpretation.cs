namespace FieldLink.PacketBench.Operations;

/// <summary>읽은 바이트를 어떤 값으로 해석할지 정합니다.</summary>
internal enum ReadInterpretation
{
    Bit,
    Word,
    Int16,
    DWord,
    Int32,
    Float32,
    Float64,
    RawBytes,
}
