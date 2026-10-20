namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta PLC 통신 전송 인코딩 방식입니다.</summary>
public enum DeltaTransportEncoding
{
    /// <summary>Modbus RTU (시리얼 또는 RTU over TCP)</summary>
    Rtu,

    /// <summary>Modbus ASCII (시리얼)</summary>
    Ascii,

    /// <summary>Modbus TCP (이더넷 MBAP 캡슐화)</summary>
    Tcp,
}
