using System;

namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta PLC 통신 옵션입니다.</summary>
public sealed class DeltaOptions
{
    private const byte MinStation = 1;
    private const byte MaxStation = 247;
    private const int MinCount = 1;
    private const int MaxModbusReadWords = 125;
    private const int MaxModbusWriteWords = 123;
    private const int MaxModbusReadBits = 2000;
    private const int MaxModbusWriteBits = 1968;
    private const byte DefaultStation = 1;
    private const int DefaultWordLimit = 120;
    private const int DefaultBitLimit = 1920;

    private readonly byte _station = DefaultStation;
    private readonly int _maxReadWords = DefaultWordLimit;
    private readonly int _maxReadBits = DefaultBitLimit;
    private readonly int _maxWriteWords = DefaultWordLimit;
    private readonly int _maxWriteBits = DefaultBitLimit;

    /// <summary>대상 Delta PLC 시리즈입니다. 기본값은 <see cref="DeltaSeries.Dvp"/>입니다.</summary>
    public DeltaSeries Series { get; init; } = DeltaSeries.Dvp;

    /// <summary>슬레이브 국번(Station 번호)입니다. 기본값은 1입니다.</summary>
    public byte Station
    {
        get => _station;
        init
        {
            if (value is < MinStation or > MaxStation)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init => _maxReadWords = ValidateLimit(value, MaxModbusReadWords, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxReadBits
    {
        get => _maxReadBits;
        init => _maxReadBits = ValidateLimit(value, MaxModbusReadBits, nameof(value), "최대 읽기 비트 수");
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init => _maxWriteWords = ValidateLimit(value, MaxModbusWriteWords, nameof(value), "최대 쓰기 워드 수");
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxWriteBits
    {
        get => _maxWriteBits;
        init => _maxWriteBits = ValidateLimit(value, MaxModbusWriteBits, nameof(value), "최대 쓰기 비트 수");
    }

    private static int ValidateLimit(int value, int maxLimit, string paramName, string label)
    {
        if (value < MinCount || value > maxLimit)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinCount}~{maxLimit} 사이여야 합니다.");
        }

        return value;
    }
}
