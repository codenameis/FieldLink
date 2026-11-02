using System;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 통신 옵션입니다.</summary>
public sealed class VigorOptions
{
    private int _maxWordsPerRequest = 64;
    private int _maxBitsPerRequest = 1024;

    /// <summary>PLC 국번(스테이션 번호, 기본 1)입니다.</summary>
    public byte StationNumber { get; set; } = 1;

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>1회 요청당 최대 워드 수(기본 64워드, 32비트 카운터는 최대 32개)입니다. (사양서 §1.7)</summary>
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

    /// <summary>1회 요청당 최대 비트 수(기본 1024비트)입니다. (사양서 §1.7)</summary>
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
