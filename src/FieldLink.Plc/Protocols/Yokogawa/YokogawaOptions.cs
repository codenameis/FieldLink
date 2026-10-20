using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 통신 옵션입니다.</summary>
public sealed class YokogawaOptions
{
    private byte _cpuNumber = 1;

    /// <summary>CPU 번호(1~4, 기본 1)입니다.</summary>
    public byte CpuNumber
    {
        get => _cpuNumber;
        set
        {
            if (value is < 1 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "CPU 번호는 1에서 4 사이여야 합니다.");
            }

            _cpuNumber = value;
        }
    }

    /// <summary>통신 타임아웃(기본 3초)입니다.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
