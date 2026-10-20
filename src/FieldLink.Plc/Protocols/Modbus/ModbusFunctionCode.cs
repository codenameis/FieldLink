namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus Application Protocol V1.1b3에 정의된 표준 기능 코드입니다.</summary>
public enum ModbusFunctionCode : byte
{
    /// <summary>코일 읽기 (0x01)</summary>
    ReadCoils = 0x01,

    /// <summary>이산 입력 읽기 (0x02)</summary>
    ReadDiscreteInputs = 0x02,

    /// <summary>보유 레지스터 읽기 (0x03)</summary>
    ReadHoldingRegisters = 0x03,

    /// <summary>입력 레지스터 읽기 (0x04)</summary>
    ReadInputRegisters = 0x04,

    /// <summary>단일 코일 쓰기 (0x05)</summary>
    WriteSingleCoil = 0x05,

    /// <summary>단일 레지스터 쓰기 (0x06)</summary>
    WriteSingleRegister = 0x06,

    /// <summary>다중 코일 쓰기 (0x0F)</summary>
    WriteMultipleCoils = 0x0F,

    /// <summary>다중 레지스터 쓰기 (0x10)</summary>
    WriteMultipleRegisters = 0x10,
}
