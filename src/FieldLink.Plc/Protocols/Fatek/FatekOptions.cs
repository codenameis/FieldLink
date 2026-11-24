using System;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 통신 옵션입니다.</summary>
public sealed class FatekOptions
{
    private const byte DefaultStation = 1;
    private const byte MinStation = 1;
    private const byte MaxStation = 254;
    private const int MinCount = 1;
    private const int MaxWordLimit = 64;
    private const int MaxBitLimit = 255;

    private readonly byte _station = DefaultStation;
    private readonly int _maxReadWords = MaxWordLimit;
    private readonly int _maxReadBits = MaxBitLimit;
    private readonly int _maxWriteWords = MaxWordLimit;
    private readonly int _maxWriteBits = MaxBitLimit;

    /// <summary>PLC 슬레이브 국번(Station 번호)입니다. 기본값은 1입니다. (1~254)</summary>
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

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init => _maxReadWords = ValidateLimit(value, MaxWordLimit, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>한 번의 요청으로 읽을 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxReadBits
    {
        get => _maxReadBits;
        init => _maxReadBits = ValidateLimit(value, MaxBitLimit, nameof(value), "최대 읽기 비트 수");
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 워드 수입니다. 기본값은 64입니다. (최대 64)</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init => _maxWriteWords = ValidateLimit(value, MaxWordLimit, nameof(value), "최대 쓰기 워드 수");
    }

    /// <summary>한 번의 요청으로 쓸 수 있는 최대 비트 수입니다. 기본값은 255입니다. (최대 255)</summary>
    public int MaxWriteBits
    {
        get => _maxWriteBits;
        init => _maxWriteBits = ValidateLimit(value, MaxBitLimit, nameof(value), "최대 쓰기 비트 수");
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
