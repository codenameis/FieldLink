using System;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 통신 프로토콜 옵션입니다.</summary>
public sealed class GESrtpOptions
{
    private const int MinLimit = 1;
    private const int MaxLimit = ushort.MaxValue;
    private const int DefaultMaxBytes = 1024;
    private const int DefaultMaxWords = 512;

    private int _maxReadBytes = DefaultMaxBytes;
    private int _maxWriteBytes = DefaultMaxBytes;
    private int _maxWordsPerRequest = DefaultMaxWords;

    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxReadBytes
    {
        get => _maxReadBytes;
        set => _maxReadBytes = ValidateLimit(value, nameof(value), "최대 읽기 바이트 수");
    }

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxWriteBytes
    {
        get => _maxWriteBytes;
        set => _maxWriteBytes = ValidateLimit(value, nameof(value), "최대 쓰기 바이트 수");
    }

    /// <summary>1회 요청당 최대 워드(16비트) 수입니다. 기본값은 512입니다.</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        set => _maxWordsPerRequest = ValidateLimit(value, nameof(value), "최대 요청 워드 수");
    }

    private static int ValidateLimit(int value, string paramName, string label)
    {
        if (value is < MinLimit or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinLimit}~{MaxLimit} 사이여야 합니다.");
        }

        return value;
    }
}
