namespace FieldLink.OpcUa;

/// <summary>OPC UA 클라이언트의 세션 수명 상태를 나타냅니다.</summary>
public enum OpcUaClientState
{
    /// <summary>초기 상태 또는 정상 종료 완료 상태입니다.</summary>
    Closed = 0,

    /// <summary>연결 및 세션 활성화(`CreateSession`/`ActivateSession`)가 진행 중입니다.</summary>
    Opening = 1,

    /// <summary>세션이 활성화되어 데이터 서비스와 구독을 사용할 수 있습니다.</summary>
    Open = 2,

    /// <summary>구독 및 세션을 정리하며 종료하는 중입니다.</summary>
    Closing = 3,

    /// <summary>KeepAlive 연속 실패 또는 치명적 세션 오류로 인해 세션이 손실된 상태입니다. 다시 열기 전에 <see cref="IOpcUaClient.CloseAsync"/>로 정리해야 합니다.</summary>
    Faulted = 4,

    /// <summary>클라이언트 인스턴스가 완전히 해제되어 더 이상 사용할 수 없습니다.</summary>
    Disposed = 5
}
