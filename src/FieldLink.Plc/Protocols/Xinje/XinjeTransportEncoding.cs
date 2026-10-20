namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 프로토콜 전송 인코딩입니다.</summary>
public enum XinjeTransportEncoding
{
    /// <summary>Serial RS-232C / RS-485 Modbus RTU (CRC16)</summary>
    Rtu,

    /// <summary>이더넷 Modbus TCP (MBAP 포트 502)</summary>
    Tcp,
}
