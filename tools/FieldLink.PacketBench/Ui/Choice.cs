namespace FieldLink.PacketBench.Ui;

/// <summary>콤보 상자에 값과 표시 이름을 함께 담기 위한 항목입니다.</summary>
internal sealed class Choice<T>
{
    public Choice(T value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public T Value { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}
