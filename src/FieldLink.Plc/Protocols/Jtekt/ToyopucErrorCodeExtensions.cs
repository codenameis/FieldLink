namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary><see cref="ToyopucErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class ToyopucErrorCodeExtensions
{
    /// <summary>오류 코드에 해당하는 한국어 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 읽은 오류 코드입니다.</param>
    /// <returns>오류 코드의 의미 설명입니다.</returns>
    public static string GetDescription(this ToyopucErrorCode errorCode) => errorCode switch
    {
        ToyopucErrorCode.Success => "정상 완료",
        ToyopucErrorCode.CpuHardwareError => "CPU 모듈 하드웨어 이상",
        ToyopucErrorCode.RelayFixedDataError => "중계 명령 고정 데이터(ENQ) 오류",
        ToyopucErrorCode.TransferByteCountError => "전송 바이트 수 오류 (헤더 길이와 본문 불일치)",
        ToyopucErrorCode.UndefinedCommand => "정의되지 않은 명령 코드 오류",
        ToyopucErrorCode.InvalidSubcommand => "서브명령 코드 오류",
        ToyopucErrorCode.CommandFormatDataError => "명령 포맷 데이터 바이트 오류",
        ToyopucErrorCode.OperandCountError => "기능 호출 피연산자 개수 오류",
        ToyopucErrorCode.WriteProtectedInRun => "시퀀스 RUN 중 쓰기 금지 또는 메모리 보호 오류",
        ToyopucErrorCode.UndefinedProgramNumber => "정의되지 않은 프로그램 번호(PRG) 오류",
        ToyopucErrorCode.AccessDeniedByParameter => "파라미터 설정에 의한 접근 금지",
        ToyopucErrorCode.ExclusiveControlOccupied => "타 스테이션의 배타 제어 점유",
        ToyopucErrorCode.CannotExecuteDuringReset => "CPU 리셋 상태로 인한 명령 실행 불가",
        ToyopucErrorCode.CannotExecuteDuringStop => "CPU 정지(STOP) 상태로 인한 명령 실행 불가",
        ToyopucErrorCode.AddressOutOfRange => "주소 범위 초과 오류",
        ToyopucErrorCode.CountOutOfRange => "워드 또는 바이트 개수 범위 초과 오류",
        _ => $"알 수 없는 TOYOPUC 오류(0x{(byte)errorCode:X2})",
    };
}
