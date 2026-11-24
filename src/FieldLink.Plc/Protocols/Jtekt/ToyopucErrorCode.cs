namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 응답의 오류 코드입니다. (T-756, 사양서 §2.3)</summary>
public enum ToyopucErrorCode : byte
{
    /// <summary>정상 완료 (0x00)</summary>
    Success = 0x00,

    /// <summary>CPU 모듈 하드웨어 이상 (0x11)</summary>
    CpuHardwareError = 0x11,

    /// <summary>중계 명령 고정 데이터(ENQ) 오류 (0x20)</summary>
    RelayFixedDataError = 0x20,

    /// <summary>전송 바이트 수 오류 — 헤더 길이와 본문 불일치 (0x21)</summary>
    TransferByteCountError = 0x21,

    /// <summary>정의되지 않은 명령 코드(CMD) 오류 (0x23)</summary>
    UndefinedCommand = 0x23,

    /// <summary>서브명령 코드 오류 (0x24)</summary>
    InvalidSubcommand = 0x24,

    /// <summary>명령 포맷 데이터 바이트 오류 (0x25)</summary>
    CommandFormatDataError = 0x25,

    /// <summary>기능 호출 피연산자 개수 오류 (0x26)</summary>
    OperandCountError = 0x26,

    /// <summary>시퀀스 RUN 중 쓰기 금지 또는 메모리 보호 (0x31)</summary>
    WriteProtectedInRun = 0x31,

    /// <summary>정의되지 않은 프로그램 번호(PRG / Ex No.) 오류 (0x32)</summary>
    UndefinedProgramNumber = 0x32,

    /// <summary>파라미터 설정에 의한 접근 금지 (0x34)</summary>
    AccessDeniedByParameter = 0x34,

    /// <summary>타 스테이션의 배타 제어 또는 실행 우선권 점유 (0x35)</summary>
    ExclusiveControlOccupied = 0x35,

    /// <summary>CPU 리셋 상태로 인한 명령 실행 불가 (0x3E)</summary>
    CannotExecuteDuringReset = 0x3E,

    /// <summary>CPU 정지(STOP) 상태로 인한 명령 실행 불가 (0x3F)</summary>
    CannotExecuteDuringStop = 0x3F,

    /// <summary>주소 범위 초과 (0x40)</summary>
    AddressOutOfRange = 0x40,

    /// <summary>읽기/쓰기 워드 또는 바이트 개수 범위 초과 (0x41)</summary>
    CountOutOfRange = 0x41,
}
