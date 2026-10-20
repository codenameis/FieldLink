namespace FieldLink.Plc.Protocols.Megmeet;

/// <summary>Megmeet Modbus 통신 프레임 인코딩 방식입니다.</summary>
public enum MegmeetTransportEncoding
{
    /// <summary>Modbus RTU (시리얼 RS-485/RS-232, CRC16).</summary>
    Rtu,

    /// <summary>Modbus TCP (이더넷 MBAP 7바이트 헤더).</summary>
    Tcp,
}
