namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 Computer Link 응답의 상태 및 오류 코드입니다. (사양서 §2.3)</summary>
public enum VigorErrorCode : byte
{
    /// <summary>정상 완료 (0x00)</summary>
    Success = 0x00,

    /// <summary>체크섬(Sum Check) 오류 (0x02)</summary>
    ChecksumError = 0x02,

    /// <summary>요청 데이터 개수가 0이거나 허용 블록 크기 초과 (0x04)</summary>
    CountOutOfRange = 0x04,

    /// <summary>디바이스 주소 범위 초과 (0x06)</summary>
    AddressOutOfRange = 0x06,

    /// <summary>ASCII 변환 오류 (0x08)</summary>
    AsciiConversionError = 0x08,

    /// <summary>존재하지 않는 기능 코드 (0x31)</summary>
    InvalidFunctionCode = 0x31,
}
