namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 PLC 가 흉내 낼 프로토콜입니다.</summary>
internal enum VirtualPlcProtocol
{
    /// <summary>Modbus TCP 서버 역할</summary>
    ModbusTcp,

    /// <summary>MC 프로토콜 3E 바이너리(Q/L) 서버 역할</summary>
    Mc3EBinary,
}
