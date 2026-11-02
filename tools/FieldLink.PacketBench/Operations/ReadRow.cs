namespace FieldLink.PacketBench.Operations;

/// <summary>읽기 결과 표의 한 줄입니다.</summary>
internal sealed class ReadRow
{
    public ReadRow(string address, string value, string hex, string binary, string ascii)
    {
        Address = address;
        Value = value;
        Hex = hex;
        Binary = binary;
        Ascii = ascii;
    }

    public string Address { get; }

    public string Value { get; }

    public string Hex { get; }

    public string Binary { get; }

    public string Ascii { get; }
}
