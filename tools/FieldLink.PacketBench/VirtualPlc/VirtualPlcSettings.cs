namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 PLC 가 요청에 응답하는 방식을 정하는 설정입니다. 실행 중에 바꾸면 다음 요청부터 적용됩니다.</summary>
internal sealed class VirtualPlcSettings
{
    /// <summary>응답할 유닛 ID 입니다. 0이면 모든 유닛 ID 에 응답합니다.</summary>
    public byte UnitId { get; set; } = 1;

    /// <summary>응답 전에 기다리는 시간입니다. 장치 지연을 흉내 낼 때 씁니다.</summary>
    public int ResponseDelayMilliseconds { get; set; }

    /// <summary>0이 아니면 모든 요청을 이 예외 코드로 거절합니다.</summary>
    public byte ForcedExceptionCode { get; set; }

    /// <summary>true 이면 아무 응답도 보내지 않습니다. 클라이언트 제한 시간 동작을 확인할 때 씁니다.</summary>
    public bool SuppressResponse { get; set; }
}
