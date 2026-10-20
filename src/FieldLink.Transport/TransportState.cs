namespace FieldLink.Transport;

/// <summary>조회 시점의 전송 연결 상태입니다.</summary>
/// <remarks>Open이라도 원격 장치가 살아 있거나 다음 요청이 성공한다는 보장은 없습니다.</remarks>
public enum TransportState
{
    /// <summary>연결되지 않았습니다. OpenAsync로 연결할 수 있습니다.</summary>
    Closed,

    /// <summary>연결을 시도하는 중입니다.</summary>
    Opening,

    /// <summary>연결되어 송수신할 수 있습니다.</summary>
    Open,

    /// <summary>해제되어 다시 사용할 수 없습니다.</summary>
    Disposed,
}
