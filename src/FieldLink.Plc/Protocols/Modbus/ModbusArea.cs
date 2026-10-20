namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus 데이터 모델의 4개 기본 메모리 영역입니다.</summary>
public enum ModbusArea
{
    /// <summary>코일 (0x, 1비트 읽기/쓰기)</summary>
    Coil,

    /// <summary>이산 입력 (1x, 1비트 읽기 전용)</summary>
    DiscreteInput,

    /// <summary>입력 레지스터 (3x, 16비트 읽기 전용)</summary>
    InputRegister,

    /// <summary>보유 레지스터 (4x, 16비트 읽기/쓰기)</summary>
    HoldingRegister,
}
