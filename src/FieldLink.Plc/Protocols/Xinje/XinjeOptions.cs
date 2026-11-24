using System;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 통신 옵션입니다.</summary>
public sealed class XinjeOptions
{
    private const byte MinStation = 1;
    private const byte MaxStation = 247;
    private const byte DefaultStation = 1;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private byte _station = DefaultStation;
    private TimeSpan _timeout = DefaultTimeout;

    /// <summary>PLC 시리즈(기본 XdXl)입니다.</summary>
    public XinjeSeries Series { get; set; } = XinjeSeries.XdXl;

    /// <summary>확장 Modbus 기능 코드(0x1E~0x21) 사용 여부(기본 true)입니다.</summary>
    public bool UseExtendedProtocol { get; set; } = true;

    /// <summary>PLC 국번(기본 1)입니다.</summary>
    public byte Station
    {
        get => _station;
        set
        {
            if (value is < MinStation or > MaxStation)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
            }

            _station = value;
        }
    }

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
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
}
