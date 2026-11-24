using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 통신 옵션입니다.</summary>
public sealed class YokogawaOptions
{
    private const byte MinCpuNumber = 1;
    private const byte MaxCpuNumber = 4;
    private const int DefaultTimeoutSeconds = 3;
    private const int MinElementsPerRequest = 1;
    private const int DefaultMaxWordsPerRequest = 64;
    private const int DefaultMaxBitsPerRequest = 256;

    private byte _cpuNumber = MinCpuNumber;
    private TimeSpan _timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    private int _maxWordsPerRequest = DefaultMaxWordsPerRequest;
    private int _maxBitsPerRequest = DefaultMaxBitsPerRequest;

    /// <summary>CPU 번호(1~4, 기본 1)입니다.</summary>
    public byte CpuNumber
    {
        get => _cpuNumber;
        set
        {
            if (value < MinCpuNumber || value > MaxCpuNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"CPU 번호는 {MinCpuNumber}에서 {MaxCpuNumber} 사이여야 합니다.");
            }

            _cpuNumber = value;
        }
    }

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

    /// <summary>1회 요청당 최대 워드 수(기본 64워드)입니다.</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 워드 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxWordsPerRequest = value;
        }
    }

    /// <summary>1회 요청당 최대 비트 수(기본 256비트)입니다.</summary>
    public int MaxBitsPerRequest
    {
        get => _maxBitsPerRequest;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 비트 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxBitsPerRequest = value;
        }
    }
}
