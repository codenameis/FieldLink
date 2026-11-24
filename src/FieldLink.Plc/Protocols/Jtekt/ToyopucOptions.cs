using System;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 통신 옵션입니다.</summary>
public sealed class ToyopucOptions
{
    private const int MinWords = 1;
    private const int MaxWords = 32765;
    private const int DefaultMaxWords = 500;

    private readonly int _maxReadWords = DefaultMaxWords;
    private readonly int _maxWriteWords = DefaultMaxWords;

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 500입니다. (1~32765)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init => _maxReadWords = ValidateWordLimit(value, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 500입니다. (1~32765)</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init => _maxWriteWords = ValidateWordLimit(value, nameof(value), "최대 쓰기 워드 수");
    }

    private static int ValidateWordLimit(int value, string paramName, string label)
    {
        if (value is < MinWords or > MaxWords)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinWords}~{MaxWords} 사이여야 합니다.");
        }

        return value;
    }
}
