namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary><see cref="MewtocolErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class MewtocolErrorCodeExtensions
{
    /// <summary>오류 코드에 해당하는 한국어 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 수신한 오류 코드입니다.</param>
    /// <returns>오류 코드의 의미 설명입니다.</returns>
    public static string GetDescription(this MewtocolErrorCode errorCode) => errorCode switch
    {
        MewtocolErrorCode.UnknownError => "알 수 없는 링크 오류입니다.",
        MewtocolErrorCode.NackError => "NACK 오류 — 전송 중 패리티 또는 프레이밍 오류가 발생했습니다.",
        MewtocolErrorCode.WackError => "WACK 오류 — 수신 버퍼가 가득 찼습니다.",
        MewtocolErrorCode.DuplicateStation => "MEWTOCOL 국번이 중복되었습니다.",
        MewtocolErrorCode.TransmissionFormatError => "전송 포맷 오류입니다.",
        MewtocolErrorCode.LinkUnitHardwareError => "링크 유닛 하드웨어 이상입니다.",
        MewtocolErrorCode.StationNumberOutOfRange => "국번 설정 범위를 벗어났습니다.",
        MewtocolErrorCode.FrameLengthExceeded => "프레임 길이가 허용 상한을 초과했습니다.",
        MewtocolErrorCode.RemoteStationNoResponse => "원격 국이 응답하지 않습니다.",
        MewtocolErrorCode.ClosedBufferAccess => "닫힌 버퍼에 접근했습니다.",
        MewtocolErrorCode.TransmissionTimeout => "전송 제한 시간이 초과되었습니다.",
        MewtocolErrorCode.OtherCommunicationError => "기타 통신 오류가 발생했습니다.",
        MewtocolErrorCode.BccError => "BCC 체크섬이 일치하지 않습니다.",
        MewtocolErrorCode.FormatError => "명령 메시지 길이 또는 문자 형식 오류입니다.",
        MewtocolErrorCode.CommandNotSupported => "지원하지 않는 명령이거나 대상 기기가 지원하지 않습니다.",
        MewtocolErrorCode.ProcedureError => "다중 프레임 처리 절차 오류입니다.",
        MewtocolErrorCode.LinkSettingError => "링크 설정 오류입니다.",
        MewtocolErrorCode.SimultaneousBufferOverflow => "동시 동작 버퍼 개수를 초과했습니다.",
        MewtocolErrorCode.CannotSend => "송신할 수 없는 상태입니다.",
        MewtocolErrorCode.Busy => "장치가 이전 요청을 처리 중입니다.",
        MewtocolErrorCode.ParameterError => "지정한 영역 코드나 파라미터가 존재하지 않습니다.",
        MewtocolErrorCode.DataError => "접점/레지스터 번호, 형식 또는 범위 지정 오류입니다.",
        MewtocolErrorCode.MonitorRegistrationError => "모니터 등록 오류입니다.",
        MewtocolErrorCode.PlcOperationModeError => "현재 PLC 운전 모드에서 실행할 수 없는 명령입니다.",
        MewtocolErrorCode.ExternalMemoryHardwareError => "외부 메모리(ROM/카세트) 하드웨어 오류입니다.",
        MewtocolErrorCode.ProtectionError => "쓰기 금지 영역이거나 암호 잠금으로 보호되어 있습니다.",
        MewtocolErrorCode.AddressError => "주소 형식 또는 시작/종료 경계 오류입니다.",
        MewtocolErrorCode.NoReadTargetData => "읽기 대상 데이터가 없습니다.",
        MewtocolErrorCode.ProgramEditProhibitedInRun => "RUN 중 프로그램 수정이 금지되어 있습니다.",
        MewtocolErrorCode.ExclusiveControlError => "다른 기능과 동시에 실행할 수 없습니다.",
        MewtocolErrorCode.InternalTimeout72 or MewtocolErrorCode.InternalTimeout73 => "내부 처리 제한 시간이 초과되었습니다.",
        MewtocolErrorCode.SdCardNotInserted => "SD 카드가 삽입되어 있지 않습니다.",
        MewtocolErrorCode.GuaranteedDataCrcError => "보장 데이터 CRC가 일치하지 않습니다.",
        MewtocolErrorCode.NoValidData => "유효한 데이터가 없습니다.",
        MewtocolErrorCode.LoggingTraceCommandError => "로깅/트레이스 실행 중 명령 오류입니다.",
        MewtocolErrorCode.UnsupportedSdCard => "지원하지 않는 SD 카드입니다.",
        _ => $"알 수 없는 MEWTOCOL 오류 코드({(int)errorCode:D2})입니다.",
    };
}
