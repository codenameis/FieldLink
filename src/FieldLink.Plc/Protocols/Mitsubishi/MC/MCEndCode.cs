namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 응답의 종료 코드(End Code) 중 사양서에 수록된 값입니다. (사양서 §6)</summary>
public enum MCEndCode : ushort
{
    /// <summary>정상 완료.</summary>
    Success = 0x0000,

    /// <summary>요청 데이터 길이 필드가 실제 프레임 크기와 맞지 않습니다.</summary>
    RequestLengthMismatch = 0x0050,

    /// <summary>RUN 중 쓰기 금지 설정 상태에서 쓰기가 요청되었습니다.</summary>
    WriteProhibitedWhileRunning = 0x0055,

    /// <summary>한 번에 읽거나 쓸 수 있는 점수 한도를 넘었습니다.</summary>
    PointCountExceeded = 0xC051,

    /// <summary>시작 또는 끝 주소가 디바이스 유효 범위를 벗어났습니다.</summary>
    DeviceRangeExceeded = 0xC056,

    /// <summary>커맨드 또는 서브커맨드 지정이 잘못되었습니다.</summary>
    InvalidCommand = 0xC059,

    /// <summary>지정한 디바이스에는 읽기·쓰기를 할 수 없습니다.</summary>
    DeviceNotAccessible = 0xC05B,

    /// <summary>요청 내용에 오류가 있습니다.</summary>
    InvalidRequest = 0xC05C,

    /// <summary>대상 CPU 모듈에서 실행할 수 없는 요청입니다.</summary>
    NotExecutableOnTarget = 0xC05F,

    /// <summary>요청 데이터 내용에 오류가 있습니다.</summary>
    InvalidRequestData = 0xC060,

    /// <summary>요청 데이터 길이가 디바이스 점수와 일치하지 않습니다.</summary>
    DataLengthMismatch = 0xC061,
}
