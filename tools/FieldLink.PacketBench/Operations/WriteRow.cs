namespace FieldLink.PacketBench.Operations;

/// <summary>쓰기 결과 표의 한 줄입니다.</summary>
internal sealed class WriteRow
{
    public WriteRow(string address, string value, string result)
    {
        Address = address;
        Value = value;
        Result = result;
    }

    public string Address { get; }

    public string Value { get; }

    public string Result { get; }
}
