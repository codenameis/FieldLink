using System;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 프로토콜의 통신 옵션입니다. (사양서 §1, §4)</summary>
public sealed class FEnetOptions
{
    private const byte DefaultCpuInfo = 0xA0; // XGK/XGB 기본 CPU 타입
    private const int DefaultPort = 2004;      // FEnet 서버 기본 TCP 포트
    private const int MinPort = 1;
    private const int MaxPort = ushort.MaxValue;
    private const int MinChunkBytes = 1;
    private const int MaxChunkBytes = ushort.MaxValue;
    private const int DefaultMaxReadBytes = 1400;  // 1회 최대 읽기 바이트
    private const int DefaultMaxWriteBytes = 1400; // 1회 최대 쓰기 바이트

    private readonly int _port = DefaultPort;
    private readonly int _maxReadBytes = DefaultMaxReadBytes;
    private readonly int _maxWriteBytes = DefaultMaxWriteBytes;

    /// <summary>기본값으로 FEnet 옵션을 초기화합니다.</summary>
    public FEnetOptions(byte cpuInfo = DefaultCpuInfo)
    {
        CpuInfo = cpuInfo;
    }

    /// <summary>대상 CPU 모델 정보 바이트입니다. (XGK/XGB: 0xA0, XGI: 0xA4)</summary>
    public byte CpuInfo { get; init; }

    /// <summary>대상 FEnet 서버 TCP 포트 번호입니다. 기본값은 2004입니다.</summary>
    public int Port
    {
        get => _port;
        init
        {
            if (value is < MinPort or > MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"포트 번호는 {MinPort}에서 {MaxPort} 사이여야 합니다.");
            }

            _port = value;
        }
    }

    /// <summary>1회 요청당 최대 읽기 바이트 수입니다. 기본값은 1400바이트입니다.</summary>
    public int MaxReadBytes
    {
        get => _maxReadBytes;
        init
        {
            if (value is < MinChunkBytes or > MaxChunkBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 읽기 바이트 수는 {MinChunkBytes}에서 {MaxChunkBytes} 사이여야 합니다.");
            }

            _maxReadBytes = value;
        }
    }

    /// <summary>1회 요청당 최대 쓰기 바이트 수입니다. 기본값은 1400바이트입니다.</summary>
    public int MaxWriteBytes
    {
        get => _maxWriteBytes;
        init
        {
            if (value is < MinChunkBytes or > MaxChunkBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 쓰기 바이트 수는 {MinChunkBytes}에서 {MaxChunkBytes} 사이여야 합니다.");
            }

            _maxWriteBytes = value;
        }
    }
}
