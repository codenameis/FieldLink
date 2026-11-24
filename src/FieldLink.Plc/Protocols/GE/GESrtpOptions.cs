using System;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 통신 프로토콜 옵션입니다.</summary>
public sealed class GESrtpOptions
{
    private const int MinBytes = 1;
    private const int MaxBytes = ushort.MaxValue;
    private const int DefaultMaxBytes = 1024;

    private int _maxReadBytes = DefaultMaxBytes;
    private int _maxWriteBytes = DefaultMaxBytes;

    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxReadBytes
    {
        get => _maxReadBytes;
        set => _maxReadBytes = ValidateByteLimit(value, nameof(value), "최대 읽기 바이트 수");
    }

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1024입니다.</summary>
    public int MaxWriteBytes
    {
        get => _maxWriteBytes;
        set => _maxWriteBytes = ValidateByteLimit(value, nameof(value), "최대 쓰기 바이트 수");
    }

    private static int ValidateByteLimit(int value, string paramName, string label)
    {
        if (value is < MinBytes or > MaxBytes)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinBytes}~{MaxBytes} 사이여야 합니다.");
        }

        return value;
    }
}
