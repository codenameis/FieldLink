using System;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 통신 옵션입니다.</summary>
public sealed class XinjeOptions
{
    private const byte MinStation = 1;
    private const byte MaxStation = 247;
    private const byte DefaultStation = 1;
    private const int MinElementsPerRequest = 1;
    private const int MaxStandardReadWords = 125;
    private const int MaxStandardWriteWords = 123;
    private const int MaxStandardReadBits = 2000;
    private const int MaxStandardWriteBits = 1968;
    private const int DefaultMaxWords = 120;
    private const int DefaultMaxBits = 1920;

    private readonly XinjeSeries _series = XinjeSeries.XdXl;
    private readonly byte _station = DefaultStation;
    private readonly int _maxReadWords = DefaultMaxWords;
    private readonly int _maxReadBits = DefaultMaxBits;
    private readonly int _maxWriteWords = DefaultMaxWords;
    private readonly int _maxWriteBits = DefaultMaxBits;

    /// <summary>대상 PLC 시리즈입니다. 기본값은 <see cref="XinjeSeries.XdXl"/>입니다.</summary>
    public XinjeSeries Series
    {
        get => _series;
        init
        {
            if (!Enum.IsDefined(typeof(XinjeSeries), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "지원하지 않는 XINJE PLC 시리즈입니다.");
            }

            _series = value;
        }
    }

    /// <summary>확장 Modbus 기능 코드(0x1E~0x21) 사용 여부입니다. 기본값은 true입니다.</summary>
    public bool UseExtendedProtocol { get; init; } = true;

    /// <summary>표준 Modbus 단일 원소 쓰기 시 FC 0x05/0x06을 사용할지 여부입니다. 기본값은 false입니다.</summary>
    public bool UseSingleWriteFunctions { get; init; }

    /// <summary>슬레이브 PLC 국번입니다. 기본값은 1입니다.</summary>
    public byte Station
    {
        get => _station;
        init
        {
            if (value < MinStation || value > MaxStation)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 120이며, 확장 프로토콜은 최대 120, 표준 Modbus는 최대 125입니다.</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        init => _maxReadWords = ValidateLimit(value, MaxStandardReadWords, nameof(value), "최대 읽기 워드 수");
    }

    /// <summary>1회 요청당 최대 읽기 비트 수입니다. 기본값은 1920이며, 확장 프로토콜은 최대 1920, 표준 Modbus는 최대 2000입니다.</summary>
    public int MaxReadBits
    {
        get => _maxReadBits;
        init => _maxReadBits = ValidateLimit(value, MaxStandardReadBits, nameof(value), "최대 읽기 비트 수");
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 120이며, 확장 프로토콜은 최대 120, 표준 Modbus는 최대 123입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        init => _maxWriteWords = ValidateLimit(value, MaxStandardWriteWords, nameof(value), "최대 쓰기 워드 수");
    }

    /// <summary>1회 요청당 최대 쓰기 비트 수입니다. 기본값은 1920이며, 확장 프로토콜은 최대 1920, 표준 Modbus는 최대 1968입니다.</summary>
    public int MaxWriteBits
    {
        get => _maxWriteBits;
        init => _maxWriteBits = ValidateLimit(value, MaxStandardWriteBits, nameof(value), "최대 쓰기 비트 수");
    }

    private static int ValidateLimit(int value, int maxLimit, string paramName, string label)
    {
        if (value < MinElementsPerRequest || value > maxLimit)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinElementsPerRequest}~{maxLimit} 범위여야 합니다.");
        }

        return value;
    }
}
