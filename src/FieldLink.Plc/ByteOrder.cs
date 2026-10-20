namespace FieldLink.Plc;

/// <summary>여러 바이트로 된 숫자(16·32·64비트)를 PLC 메모리에 배치하는 순서입니다.</summary>
/// <remarks>
/// 32비트 값 0x0A0B0C0D를 바이트 A=0x0A, B=0x0B, C=0x0C, D=0x0D로 볼 때 메모리에 놓이는 순서입니다.
/// 업계에서 흔히 쓰는 ABCD 표기를 함께 적었습니다.
/// </remarks>
public enum ByteOrder
{
    /// <summary>ABCD. 큰 자리부터 저장합니다. (Modbus 레지스터 기본, Siemens S7)</summary>
    BigEndian,

    /// <summary>CDAB. 워드(2바이트) 안은 큰 자리부터, 워드는 작은 자리부터 저장합니다. (OMRON FINS)</summary>
    BigEndianWordSwap,

    /// <summary>DCBA. 작은 자리부터 저장합니다. (Mitsubishi MC, LS ELECTRIC XGT, Allen-Bradley CIP)</summary>
    LittleEndian,

    /// <summary>BADC. 워드 안은 작은 자리부터, 워드는 큰 자리부터 저장합니다.</summary>
    LittleEndianWordSwap,
}
