using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 통신 옵션입니다.</summary>
public sealed class YokogawaOptions
{
    private const byte MinCpuNumber = 1;
    private const byte MaxCpuNumber = 4;
    private const int MinElementsPerRequest = 1;
    private const int MaxWordsLimit = 32763;
    private const int MaxBitsLimit = 65527;
    private const int DefaultMaxWordsPerRequest = 64;
    private const int DefaultMaxBitsPerRequest = 256;

    private readonly byte _cpuNumber = MinCpuNumber;
    private readonly int _maxWordsPerRequest = DefaultMaxWordsPerRequest;
    private readonly int _maxBitsPerRequest = DefaultMaxBitsPerRequest;

    /// <summary>CPU 번호(1~4, 기본 1)입니다.</summary>
    public byte CpuNumber
    {
        get => _cpuNumber;
        init
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

    /// <summary>1회 요청당 최대 워드 수(1~32763, 기본 64워드)입니다.</summary>
    public int MaxWordsPerRequest
    {
        get => _maxWordsPerRequest;
        init
        {
            if (value < MinElementsPerRequest || value > MaxWordsLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 워드 수는 {MinElementsPerRequest}~{MaxWordsLimit} 범위여야 합니다.");
            }

            _maxWordsPerRequest = value;
        }
    }

    /// <summary>1회 요청당 최대 비트 수(1~65527, 기본 256비트)입니다.</summary>
    public int MaxBitsPerRequest
    {
        get => _maxBitsPerRequest;
        init
        {
            if (value < MinElementsPerRequest || value > MaxBitsLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 비트 수는 {MinElementsPerRequest}~{MaxBitsLimit} 범위여야 합니다.");
            }

            _maxBitsPerRequest = value;
        }
    }
}
