namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 로더 프로토콜 응답의 종료·오류 코드입니다. (사양서 Part A §A.2.3)</summary>
public enum FujiElectricSpbErrorCode : byte
{
    /// <summary>정상 완료 (0x00)</summary>
    Normal = 0x00,

    /// <summary>ROM 영역 쓰기 요청 오류 (0x01)</summary>
    RomAreaWriteError = 0x01,

    /// <summary>정의되지 않았거나 처리 불가한 명령/서브명령 (0x02)</summary>
    InvalidCommand = 0x02,

    /// <summary>데이터 영역 매개변수 지정 오류 (0x03)</summary>
    DataAreaParameterError = 0x03,

    /// <summary>타 로더/프로그래머의 전송 인터록 점유 (0x04)</summary>
    TransferInterlockOccupied = 0x04,

    /// <summary>모듈 번호 지정 오류 (0x05)</summary>
    InvalidModuleNumber = 0x05,

    /// <summary>검색 항목 없음 (0x06)</summary>
    SearchItemNotFound = 0x06,

    /// <summary>쓰기 주소가 모듈 메모리 범위 초과 (0x07)</summary>
    AddressOutOfRange = 0x07,

    /// <summary>프로그램 오류로 인해 RUN 전환 불가 (0x09)</summary>
    ProgramErrorCannotRun = 0x09,

    /// <summary>암호 불일치 (0x0C)</summary>
    PasswordMismatch = 0x0C,
}
