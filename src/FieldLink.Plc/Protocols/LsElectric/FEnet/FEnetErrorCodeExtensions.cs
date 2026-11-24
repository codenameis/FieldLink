namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

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
