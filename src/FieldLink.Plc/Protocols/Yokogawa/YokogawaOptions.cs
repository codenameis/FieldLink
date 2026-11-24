using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 통신 옵션입니다.</summary>
public sealed class YokogawaOptions
{
    private const byte MinCpuNumber = 1;
    private const byte MaxCpuNumber = 4;
    private const int DefaultTimeoutSeconds = 3;

    private byte _cpuNumber = MinCpuNumber;
    private TimeSpan _timeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    /// <summary>CPU 번호(1~4, 기본 1)입니다.</summary>
    public byte CpuNumber
    {
        get => _cpuNumber;
        set
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

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "타임아웃은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }
}
