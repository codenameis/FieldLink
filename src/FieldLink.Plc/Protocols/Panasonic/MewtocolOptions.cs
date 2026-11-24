using System;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 통신 옵션입니다.</summary>
public sealed class MewtocolOptions
{
    private const byte MinStationNumber = 1;
    private const byte MaxStationNumber = 99;
    private const byte DirectConnectionStation = 0xEE;
    private const int MinWordsPerRequest = 1;
    private const int MaxWordsPerRequest = 500;

    private readonly byte _station = DirectConnectionStation;
    private readonly int _maxReadWords = MaxWordsPerRequest;
    private readonly int _maxWriteWords = MaxWordsPerRequest;

    /// <summary>PLC 슬레이브 국번(1~99, 또는 1:1 직결 국번 0xEE)입니다. 기본값은 0xEE입니다. (사양서 §1.1)</summary>
    public byte Station
    {
        get => _station;
        init
        {
            if (value != DirectConnectionStation && (value < MinStationNumber || value > MaxStationNumber))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"국번은 {MinStationNumber}~{MaxStationNumber} 또는 0x{DirectConnectionStation:X2}여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 500입니다. (사양서 §1.6)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init
        {
            if (value < MinWordsPerRequest || value > MaxWordsPerRequest)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 읽기 워드 수는 {MinWordsPerRequest}~{MaxWordsPerRequest} 범위여야 합니다.");
            }

            _maxReadWords = value;
        }
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 500입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init
        {
            if (value < MinWordsPerRequest || value > MaxWordsPerRequest)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"최대 쓰기 워드 수는 {MinWordsPerRequest}~{MaxWordsPerRequest} 범위여야 합니다.");
            }

            _maxWriteWords = value;
        }
    }
}
