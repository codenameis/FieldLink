using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 통신 옵션입니다.</summary>
public sealed class YokogawaOptions
{
    private byte _cpuNumber = 1;
    private int _maxWordsPerRequest = 64;
    private int _maxBitsPerRequest = 256;

    /// <summary>CPU 번호(1~4, 기본 1)입니다.</summary>
    public byte CpuNumber
    {
        get => _cpuNumber;
        set
        {
            if (value is < 1 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "CPU 번호는 1에서 4 사이여야 합니다.");
            }

            _cpuNumber = value;
        }
    }

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>1회 요청당 최대 워드 수(기본 64워드)입니다.</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 워드 수는 1 이상이어야 합니다.");
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
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 비트 수는 1 이상이어야 합니다.");
            }

            _maxBitsPerRequest = value;
        }
    }
}
