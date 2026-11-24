namespace FieldLink.Plc.Protocols.Vigor;

/// <summary><see cref="VigorErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class VigorErrorCodeExtensions
{
    /// <summary>오류 코드에 해당하는 한국어 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 읽은 오류 코드입니다.</param>
    /// <returns>오류 코드의 의미 설명입니다.</returns>
    public static string GetDescription(this VigorErrorCode errorCode) => errorCode switch
    {
        VigorErrorCode.Success => "정상 완료",
        VigorErrorCode.ChecksumError => "체크섬(Sum Check) 오류입니다. (0x02)",
        VigorErrorCode.CountOutOfRange => "요청 데이터 개수가 0이거나 허용 블록 크기를 초과했습니다. (0x04)",
        VigorErrorCode.AddressOutOfRange => "디바이스 주소 범위 초과 오류입니다. (0x06)",
        VigorErrorCode.AsciiConversionError => "ASCII 변환 오류입니다. (0x08)",
        VigorErrorCode.InvalidFunctionCode => "존재하지 않거나 지원하지 않는 기능 코드입니다. (0x31)",
        _ => $"VIGOR 오류 코드: 0x{(byte)errorCode:X2}",
    };
}
