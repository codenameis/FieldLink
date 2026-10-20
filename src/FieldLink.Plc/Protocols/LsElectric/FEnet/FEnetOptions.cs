namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 프로토콜의 통신 옵션입니다. (사양서 §1, §4)</summary>
public sealed class FEnetOptions
{
    private const byte DefaultCpuInfo = 0xA0; // XGK/XGB 기본 CPU 타입
    private const int DefaultPort = 2004;      // FEnet 서버 기본 TCP 포트
    private const int DefaultMaxReadBytes = 1400;  // 1회 최대 읽기 바이트
    private const int DefaultMaxWriteBytes = 1400; // 1회 최대 쓰기 바이트

    /// <summary>기본값으로 FEnet 옵션을 초기화합니다.</summary>
    public FEnetOptions(byte cpuInfo = DefaultCpuInfo)
    {
        CpuInfo = cpuInfo;
        Port = DefaultPort;
        MaxReadBytes = DefaultMaxReadBytes;
        MaxWriteBytes = DefaultMaxWriteBytes;
    }

    /// <summary>대상 CPU 모델 정보 바이트입니다. (XGK/XGB: 0xA0, XGI: 0xA4)</summary>
    public byte CpuInfo { get; init; }

    /// <summary>대상 FEnet 서버 TCP 포트 번호입니다. 기본값은 2004입니다.</summary>
    public int Port { get; init; }

    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1400바이트입니다.</summary>
    public int MaxReadBytes { get; init; }

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1400바이트입니다.</summary>
    public int MaxWriteBytes { get; init; }
}
