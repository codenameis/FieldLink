using System;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 통신 프로토콜 옵션입니다.</summary>
public sealed class GESrtpOptions
{
    private const int MinLimit = 1;
    private const int MaxExtendedPayloadBytes = 65480;
    private const int MaxWordsLimit = 32740;
    private const int DefaultMaxBytes = 1024;
    private const int DefaultMaxWords = 512;

    private readonly int _maxReadBytes = DefaultMaxBytes;
    private readonly int _maxWriteBytes = DefaultMaxBytes;
    private readonly int _maxWordsPerRequest = DefaultMaxWords;

    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1024입니다. (1~65480)</summary>
    public int MaxReadBytes
    {
        get => _maxReadBytes;
        init => _maxReadBytes = ValidateLimit(value, MaxExtendedPayloadBytes, nameof(value), "최대 읽기 바이트 수");
    }

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1024입니다. (1~65480)</summary>
    public int MaxWriteBytes
    {
        get => _maxWriteBytes;
        init => _maxWriteBytes = ValidateLimit(value, MaxExtendedPayloadBytes, nameof(value), "최대 쓰기 바이트 수");
    }

    /// <summary>1회 요청당 최대 워드(16비트) 수입니다. 기본값은 512입니다. (1~32740)</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        init => _maxWordsPerRequest = ValidateLimit(value, MaxWordsLimit, nameof(value), "최대 요청 워드 수");
    }

    private static int ValidateLimit(int value, int maxLimit, string paramName, string label)
    {
        if (value < MinLimit || value > maxLimit)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinLimit}~{maxLimit} 사이여야 합니다.");
        }

        return value;
    }
}
