namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 읽기·쓰기 응답 데이터 구역의 반환 코드입니다. (사양서 §7)</summary>
public enum S7CommReturnCode : byte
{
    /// <summary>성공.</summary>
    Success = 0xFF,

    /// <summary>하드웨어 오류.</summary>
    HardwareFault = 0x01,

    /// <summary>해당 객체에 접근이 허용되지 않습니다.</summary>
    AccessNotAllowed = 0x03,

    /// <summary>주소가 올바르지 않습니다.</summary>
    InvalidAddress = 0x05,

    /// <summary>지원하지 않는 자료형입니다.</summary>
    DataTypeNotSupported = 0x06,

    /// <summary>자료형이 일치하지 않습니다.</summary>
    DataTypeInconsistent = 0x07,

    /// <summary>객체가 존재하지 않습니다. (예: 없는 DB)</summary>
    ObjectDoesNotExist = 0x0A,
}
