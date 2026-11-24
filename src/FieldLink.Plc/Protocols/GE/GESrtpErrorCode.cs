namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 응답의 상태 및 오류 코드입니다. (GFK-2224 / GFK-0582 §2.3)</summary>
public enum GESrtpErrorCode : ushort
{
    /// <summary>정상 완료 (0x0000).</summary>
    Success = 0x0000,

    /// <summary>PLC가 서비스 요청을 거부했습니다 (메시지 타입 0xD1).</summary>
    ServiceRejected = 0xD100,
}
