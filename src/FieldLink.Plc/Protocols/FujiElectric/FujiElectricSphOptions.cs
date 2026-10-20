namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 로더 통신 옵션입니다. (사양서 Part C)</summary>
public sealed class FujiElectricSphOptions
{
    /// <summary>SX-Bus 연결 스테이션 ID(0x01~0xFE)입니다. 기본값은 1입니다. (오프셋 6)</summary>
    public byte ConnectionId { get; set; } = 1;

    /// <summary>1회 요청당 최대 읽기 워드 수입니다. 기본값은 230입니다. (사양서 §C.1.3)</summary>
    public int MaxReadWords { get; set; } = 230;

    /// <summary>1회 요청당 최대 쓰기 워드 수입니다. 기본값은 230입니다.</summary>
    public int MaxWriteWords { get; set; } = 230;
}
