using System;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜의 프레임 종류·시리즈·액세스 경로·분할 한도 설정입니다. (사양서 §3-1, §5)</summary>
public sealed class MCOptions
{
    private const byte DefaultNetworkNumber = 0x00;
    private const byte DefaultPcNumber = 0xFF;
    private const ushort DefaultModuleIoNumber = 0x03FF;
    private const byte DefaultModuleStationNumber = 0x00;
    private const ushort DefaultMonitoringTimer = 0x0010; // 250ms 단위, 4초
    private const int DefaultMaxWordPoints = 960;
    private const int DefaultMaxBitPoints = 3584;
    private const int DefaultPort = 5000;

    /// <summary>프레임 종류와 시리즈로 옵션을 만듭니다. 나머지는 사양서 기본값입니다.</summary>
    /// <param name="frameType">3E 또는 4E 프레임입니다.</param>
    /// <param name="series">대상 PLC 시리즈입니다.</param>
    /// <param name="maxWordPoints">한 번에 읽고 쓸 최대 워드 수입니다. 1 이상.</param>
    /// <param name="maxBitPoints">한 번에 읽고 쓸 최대 비트 수입니다. 1 이상.</param>
    /// <exception cref="ArgumentOutOfRangeException">한도가 1보다 작을 때.</exception>
    public MCOptions(
        MCFrameType frameType = MCFrameType.Frame3E,
        MCSeries series = MCSeries.QL,
        int maxWordPoints = DefaultMaxWordPoints,
        int maxBitPoints = DefaultMaxBitPoints)
    {
        if (maxWordPoints is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWordPoints), maxWordPoints, "워드 한도는 1 이상 65535 이하여야 합니다.");
        }

        if (maxBitPoints is < 1 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBitPoints), maxBitPoints, "비트 한도는 1 이상 65535 이하여야 합니다.");
        }

        FrameType = frameType;
        Series = series;
        MaxWordPoints = maxWordPoints;
        MaxBitPoints = maxBitPoints;
        NetworkNumber = DefaultNetworkNumber;
        PcNumber = DefaultPcNumber;
        ModuleIoNumber = DefaultModuleIoNumber;
        ModuleStationNumber = DefaultModuleStationNumber;
        MonitoringTimer = DefaultMonitoringTimer;
        Port = DefaultPort;
    }

    /// <summary>3E 또는 4E 프레임 종류입니다.</summary>
    public MCFrameType FrameType { get; }

    /// <summary>대상 PLC 시리즈입니다.</summary>
    public MCSeries Series { get; }

    /// <summary>한 번에 읽고 쓸 최대 워드 수입니다.</summary>
    public int MaxWordPoints { get; }

    /// <summary>한 번에 읽고 쓸 최대 비트 수입니다.</summary>
    public int MaxBitPoints { get; }

    /// <summary>요청 대상 네트워크 번호입니다. 기본값 0x00(자국).</summary>
    public byte NetworkNumber { get; init; }

    /// <summary>요청 대상 PC 번호입니다. 기본값 0xFF(자국).</summary>
    public byte PcNumber { get; init; }

    /// <summary>요청 대상 모듈 I/O 번호입니다. 기본값 0x03FF(CPU 모듈).</summary>
    public ushort ModuleIoNumber { get; init; }

    /// <summary>요청 대상 모듈 국번입니다. 기본값 0x00.</summary>
    public byte ModuleStationNumber { get; init; }

    /// <summary>CPU 감시 타이머입니다. 250ms 단위이며 기본값 0x0010(4초)입니다.</summary>
    public ushort MonitoringTimer { get; init; }

    /// <summary>대상 장비의 TCP·UDP 포트입니다. 장비 파라미터 설정에 따르며 기본값은 5000입니다.</summary>
    public int Port { get; init; }
}
