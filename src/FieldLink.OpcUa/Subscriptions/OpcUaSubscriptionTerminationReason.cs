namespace FieldLink.OpcUa.Subscriptions;

/// <summary>OPC UA 구독이 종료된 원인을 나타냅니다.</summary>
public enum OpcUaSubscriptionTerminationReason
{
    /// <summary>아직 종료되지 않고 활성 상태입니다.</summary>
    None = 0,

    /// <summary>사용자가 <see cref="OpcUaSubscription.CloseAsync"/>를 호출하여 정상 종료되었습니다.</summary>
    Closed = 1,

    /// <summary>로컬 유한 큐의 배치 개수 또는 바이트 상한을 초과하여 Fail-Closed 정책으로 종료되었습니다.</summary>
    QueueOverflow = 2,

    /// <summary>부모 클라이언트가 <see cref="IOpcUaClient.CloseAsync"/>로 정상 종료되면서 함께 종료되었습니다.</summary>
    ParentClosed = 3,

    /// <summary>부모 세션 손실(<see cref="OpcUaClientState.Faulted"/>) 또는 세션 세대 변경으로 종료되었습니다.</summary>
    ParentFaulted = 4,

    /// <summary>구독 또는 부모 클라이언트가 <see cref="System.IDisposable.Dispose"/>되어 해제되었습니다.</summary>
    Disposed = 5
}
