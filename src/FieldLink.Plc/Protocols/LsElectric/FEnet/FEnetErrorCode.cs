namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT 전용 통신(FEnet)의 오류 코드 상수 및 설명입니다. (사양서 §6)</summary>
public enum FEnetErrorCode : ushort
{
    /// <summary>정상 완료</summary>
    None = 0x0000,

    /// <summary>지원하지 않는 명령어입니다.</summary>
    UnsupportedCommand = 0x0003,

    /// <summary>지원하지 않는 데이터 타입입니다.</summary>
    UnsupportedDataType = 0x0004,

    /// <summary>디바이스(변수) 영역이 유효하지 않습니다.</summary>
    DeviceAreaError = 0x0010,

    /// <summary>데이터 영역 크기 또는 범위를 초과했습니다.</summary>
    DataRangeExceeded = 0x0011,

    /// <summary>요청 데이터 개수가 한도를 초과했습니다.</summary>
    DataCountExceeded = 0x0012,

    /// <summary>데이터 타입이 불일치합니다.</summary>
    DataTypeMismatch = 0x0024,

    /// <summary>헤더 Company ID가 일치하지 않습니다.</summary>
    CompanyIdMismatch = 0x0075,

    /// <summary>헤더 길이 필드가 실제 수신 크기와 일치하지 않습니다.</summary>
    LengthMismatch = 0x0076,

    /// <summary>헤더 체크섬(BCC) 검증에 실패했습니다.</summary>
    ChecksumError = 0x0077,

    /// <summary>알 수 없는 명령어를 수신했습니다.</summary>
    UnknownCommand = 0x0078
}

/// <summary><see cref="FEnetErrorCode"/>의 의미를 해석하는 확장 메서드입니다.</summary>
public static class FEnetErrorCodeExtensions
{
    /// <summary>오류 코드에 대한 한국어 설명을 반환합니다.</summary>
    public static string GetDescription(this FEnetErrorCode errorCode)
    {
        return errorCode switch
        {
            FEnetErrorCode.None => "정상",
            FEnetErrorCode.UnsupportedCommand => "지원하지 않는 명령어입니다.",
            FEnetErrorCode.UnsupportedDataType => "지원하지 않는 데이터 타입입니다.",
            FEnetErrorCode.DeviceAreaError => "디바이스(변수) 영역이 잘못되었습니다.",
            FEnetErrorCode.DataRangeExceeded => "데이터 영역 범위를 초과했습니다.",
            FEnetErrorCode.DataCountExceeded => "요청 데이터 개수가 한도를 초과했습니다.",
            FEnetErrorCode.DataTypeMismatch => "데이터 타입이 일치하지 않습니다.",
            FEnetErrorCode.CompanyIdMismatch => "헤더 Company ID가 'LSIS-XGT'가 아닙니다.",
            FEnetErrorCode.LengthMismatch => "헤더 길이 정보가 실제 프레임 크기와 일치하지 않습니다.",
            FEnetErrorCode.ChecksumError => "헤더 체크섬(BCC) 검증 실패입니다.",
            FEnetErrorCode.UnknownCommand => "알 수 없는 명령 코드입니다.",
            _ => $"알 수 없는 XGT 오류 (0x{(ushort)errorCode:X4})"
        };
    }
}
