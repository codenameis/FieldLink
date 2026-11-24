namespace FieldLink.Plc.Protocols.GE;

/// <summary><see cref="GESrtpErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class GESrtpErrorCodeExtensions
{
    /// <summary>상태 코드 또는 거부 응답 코드의 의미 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 읽은 오류 코드입니다.</param>
    /// <returns>오류 코드에 대한 한국어 설명입니다.</returns>
    public static string GetDescription(this GESrtpErrorCode errorCode) => errorCode switch
    {
        GESrtpErrorCode.Success => "정상 완료",
        GESrtpErrorCode.ServiceRejected => "SRTP 장치가 서비스 요청을 거부했습니다 (메시지 타입: 0xD1).",
        _ => $"SRTP 장치 처리 오류가 발생했습니다 (상태 코드: 0x{(ushort)errorCode:X4}).",
    };
}
