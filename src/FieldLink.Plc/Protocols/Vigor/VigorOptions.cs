using System;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 통신 옵션입니다.</summary>
public sealed class VigorOptions
{
    private const byte DefaultStationNumber = 1;
    private const int DefaultTimeoutSeconds = 3;
    private const int MinElementsPerRequest = 1;
    private const int DefaultMaxWordsPerRequest = 64;
    private const int DefaultMaxBitsPerRequest = 1024;

    private TimeSpan _timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    private int _maxWordsPerRequest = DefaultMaxWordsPerRequest;
    private int _maxBitsPerRequest = DefaultMaxBitsPerRequest;

    /// <summary>PLC 국번(스테이션 번호, 기본 1)입니다.</summary>
    public byte StationNumber { get; set; } = DefaultStationNumber;

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "타임아웃은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }

    /// <summary>1회 요청당 최대 워드 수(기본 64워드, 32비트 카운터는 최대 32개)입니다. (사양서 §1.7)</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 워드 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxWordsPerRequest = value;
        }
    }

    /// <summary>1회 요청당 최대 비트 수(기본 1024비트)입니다. (사양서 §1.7)</summary>
    public int MaxBitsPerRequest
    {
        get => _maxBitsPerRequest;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 비트 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxBitsPerRequest = value;
        }
    }
}
