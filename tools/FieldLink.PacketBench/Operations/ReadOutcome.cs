using System.Collections.Generic;

namespace FieldLink.PacketBench.Operations;

/// <summary>읽기 한 번의 결과입니다.</summary>
internal sealed class ReadOutcome
{
    public ReadOutcome(
        IReadOnlyList<ReadRow> rows,
        byte[] rawBytes,
        int elementCount,
        int byteCount,
        string summary)
    {
        Rows = rows;
        RawBytes = rawBytes;
        ElementCount = elementCount;
        ByteCount = byteCount;
        Summary = summary;
    }

    public IReadOnlyList<ReadRow> Rows { get; }

    /// <summary>PLC 메모리 순서 그대로의 바이트입니다.</summary>
    public byte[] RawBytes { get; }

    public int ElementCount { get; }

    public int ByteCount { get; }

    public string Summary { get; }
}
