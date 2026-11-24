using System;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 통신 옵션입니다.</summary>
public sealed class VigorOptions
{
    private const byte DefaultStationNumber = 1;
    private const int MinElementsPerRequest = 1;
    private const int MaxWordsLimit = 64;
    private const int MaxBitsLimit = 1024;

    private readonly byte _stationNumber = DefaultStationNumber;
    private readonly int _maxWordsPerRequest = MaxWordsLimit;
    private readonly int _maxBitsPerRequest = MaxBitsLimit;

    /// <summary>PLC 국번(스테이션 번호, 기본 1)입니다.</summary>
    public byte StationNumber
    {
        get => _stationNumber;
        init => _stationNumber = value;
    }

    /// <summary>1회 요청당 최대 워드 수(1~64, 기본 64워드, 32비트 카운터는 최대 32개)입니다. (사양서 §1.7)</summary>
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

    /// <summary>1회 요청당 최대 비트 수(1~1024, 기본 1024비트)입니다. (사양서 §1.7)</summary>
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
