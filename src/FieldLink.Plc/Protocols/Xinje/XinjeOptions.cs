using System;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 통신 옵션입니다.</summary>
public sealed class XinjeOptions
{
    private const byte MinStation = 1;
    private const byte MaxStation = 247;
    private const byte DefaultStation = 1;
    private const int MinElementsPerRequest = 1;
    private const int DefaultMaxWords = 120;
    private const int DefaultMaxBits = 1920;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private byte _station = DefaultStation;
    private TimeSpan _timeout = DefaultTimeout;
    private int _maxReadWords = DefaultMaxWords;
    private int _maxReadBits = DefaultMaxBits;
    private int _maxWriteWords = DefaultMaxWords;
    private int _maxWriteBits = DefaultMaxBits;

    /// <summary>대상 PLC 시리즈입니다. 기본값은 <see cref="XinjeSeries.XdXl"/>입니다.</summary>
    public XinjeSeries Series { get; set; } = XinjeSeries.XdXl;

    /// <summary>확장 Modbus 기능 코드(0x1E~0x21) 사용 여부입니다. 기본값은 true입니다.</summary>
    public bool UseExtendedProtocol { get; set; } = true;

    /// <summary>슬레이브 PLC 국번입니다. 기본값은 1입니다.</summary>
    public byte Station
    {
        get => _station;
        set
        {
            if (value < MinStation || value > MaxStation)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>통신 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value <= TimeSpan.Zero && value != System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "타임아웃은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 120입니다. (매뉴얼 §A.1.5)</summary>
    public int MaxReadWords
    {
        get => _maxReadWords;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 읽기 워드 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxReadWords = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 비트 수입니다. 기본값은 1920입니다. (매뉴얼 §A.1.5)</summary>
    public int MaxReadBits
    {
        get => _maxReadBits;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 읽기 비트 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxReadBits = value;
        }
    }

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 120입니다.</summary>
    public int MaxWriteWords
    {
        get => _maxWriteWords;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 쓰기 워드 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxWriteWords = value;
        }
    }

    /// <summary>1회 요청당 최대 쓰기 비트 수입니다. 기본값은 1920입니다.</summary>
    public int MaxWriteBits
    {
        get => _maxWriteBits;
        set
        {
            if (value < MinElementsPerRequest || value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 쓰기 비트 수는 {MinElementsPerRequest}~{ushort.MaxValue} 범위여야 합니다.");
            }

            _maxWriteBits = value;
        }
    }
}
