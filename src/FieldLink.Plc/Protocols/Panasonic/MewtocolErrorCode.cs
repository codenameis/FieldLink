namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 응답의 오류 코드입니다. (WUME-MEWCP-03 Chapter 3, 사양서 §2.2)</summary>
public enum MewtocolErrorCode
{
    /// <summary>링크 오류: 알 수 없는 오류 (20)</summary>
    UnknownError = 20,

    /// <summary>링크 오류: NACK 오류 — 전송 패리티·프레이밍 등 데이터 오류 (21)</summary>
    NackError = 21,

    /// <summary>링크 오류: WACK 오류 — 수신 버퍼 오버플로 (22)</summary>
    WackError = 22,

    /// <summary>링크 오류: MEWTOCOL 국번 중복 (23)</summary>
    DuplicateStation = 23,

    /// <summary>링크 오류: 전송 포맷 오류 (24)</summary>
    TransmissionFormatError = 24,

    /// <summary>링크 오류: 링크 유닛 하드웨어 이상 (25)</summary>
    LinkUnitHardwareError = 25,

    /// <summary>링크 오류: 국번 설정 범위 오류 (26)</summary>
    StationNumberOutOfRange = 26,

    /// <summary>링크 오류: 프레임 길이 초과 (27)</summary>
    FrameLengthExceeded = 27,

    /// <summary>링크 오류: 원격 국 무응답 (28)</summary>
    RemoteStationNoResponse = 28,

    /// <summary>링크 오류: 닫힌 버퍼 접근 오류 (29)</summary>
    ClosedBufferAccess = 29,

    /// <summary>링크 오류: 전송 타임아웃 (30)</summary>
    TransmissionTimeout = 30,

    /// <summary>링크 오류: 기타 통신 오류 (38)</summary>
    OtherCommunicationError = 38,

    /// <summary>기본 절차 오류: BCC 체크섬 오류 (40)</summary>
    BccError = 40,

    /// <summary>기본 절차 오류: 명령 포맷 오류 (41)</summary>
    FormatError = 41,

    /// <summary>기본 절차 오류: 미지원 명령 또는 대상 기기 미지원 (42)</summary>
    CommandNotSupported = 42,

    /// <summary>기본 절차 오류: 절차 오류 (43)</summary>
    ProcedureError = 43,

    /// <summary>처리 오류: 링크 설정 오류 (50)</summary>
    LinkSettingError = 50,

    /// <summary>처리 오류: 동시 동작 버퍼 초과 (51)</summary>
    SimultaneousBufferOverflow = 51,

    /// <summary>처리 오류: 송신 불가 (52)</summary>
    CannotSend = 52,

    /// <summary>처리 오류: 처리 중 (53)</summary>
    Busy = 53,

    /// <summary>응용 오류: 파라미터 오류 (60)</summary>
    ParameterError = 60,

    /// <summary>응용 오류: 데이터 오류 (61)</summary>
    DataError = 61,

    /// <summary>응용 오류: 모니터 등록 오류 (62)</summary>
    MonitorRegistrationError = 62,

    /// <summary>응용 오류: PLC 운전 모드 오류 (63)</summary>
    PlcOperationModeError = 63,

    /// <summary>응용 오류: 외부 메모리 하드웨어 오류 (64)</summary>
    ExternalMemoryHardwareError = 64,

    /// <summary>응용 오류: 보호 오류 (65)</summary>
    ProtectionError = 65,

    /// <summary>응용 오류: 주소 오류 (66)</summary>
    AddressError = 66,

    /// <summary>응용 오류: 읽기 대상 데이터 없음 (67)</summary>
    NoReadTargetData = 67,

    /// <summary>응용 오류: RUN 중 프로그램 수정 금지 오류 (68)</summary>
    ProgramEditProhibitedInRun = 68,

    /// <summary>응용 오류: 배타 제어 오류 (71)</summary>
    ExclusiveControlError = 71,

    /// <summary>응용 오류: 내부 처리 타임아웃 (72)</summary>
    InternalTimeout72 = 72,

    /// <summary>응용 오류: 내부 처리 타임아웃 (73)</summary>
    InternalTimeout73 = 73,

    /// <summary>응용 오류: SD 카드 미삽입 (78)</summary>
    SdCardNotInserted = 78,

    /// <summary>보장 데이터 오류: CRC 오류 (80)</summary>
    GuaranteedDataCrcError = 80,

    /// <summary>보장 데이터 오류: 유효 데이터 없음 (81)</summary>
    NoValidData = 81,

    /// <summary>로깅/트레이스 오류: 실행 중 명령 오류 (90)</summary>
    LoggingTraceCommandError = 90,

    /// <summary>로깅/트레이스 오류: 미지원 SD 카드 (92)</summary>
    UnsupportedSdCard = 92,
}
