namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus 예외 코드에 대한 설명 확장 메서드입니다.</summary>
public static class ModbusExceptionCodeExtensions
{
    /// <summary>예외 코드의 공식 규격 의미 설명을 반환합니다.</summary>
    public static string GetDescription(this ModbusExceptionCode code) => code switch
    {
        ModbusExceptionCode.IllegalFunction => "장치가 지원하지 않거나 허용하지 않는 기능 코드입니다.",
        ModbusExceptionCode.IllegalDataAddress => "요청한 주소가 장치에 유효하지 않은 범위입니다.",
        ModbusExceptionCode.IllegalDataValue => "요청한 데이터 값 또는 수량이 허용 범위를 벗어났습니다.",
        ModbusExceptionCode.ServerDeviceFailure => "장치 내부에서 복구할 수 없는 오류가 발생했습니다.",
        ModbusExceptionCode.Acknowledge => "장치가 요청을 수락하여 처리 중입니다.",
        ModbusExceptionCode.ServerDeviceBusy => "장치가 다른 작업을 수행 중이어서 지금 응답할 수 없습니다.",
        ModbusExceptionCode.MemoryParityError => "장치 메모리 일관성 검사에 실패했습니다.",
        ModbusExceptionCode.GatewayPathUnavailable => "게이트웨이가 대상 경로를 할당하지 못했습니다.",
        ModbusExceptionCode.GatewayTargetDeviceFailedToRespond => "게이트웨이 뒤의 대상 장치가 응답하지 않았습니다.",
        _ => $"알 수 없는 Modbus 예외 코드(0x{(byte)code:X2})입니다."
    };
}
