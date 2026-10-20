namespace FieldLink.Transport;

/// <summary>전송 계층에서 발생한 실패의 종류입니다.</summary>
public enum TransportFailure
{
    /// <summary>연결을 수립하지 못했습니다. (호스트 없음, 거부, 포트 열기 실패 등)</summary>
    ConnectFailed,

    /// <summary>연결이 상대방 또는 로컬에서 끊어졌습니다.</summary>
    ConnectionClosed,

    /// <summary>요청 바이트를 끝까지 보내지 못했습니다.</summary>
    SendFailed,

    /// <summary>응답을 받는 중 입출력 오류가 났습니다.</summary>
    ReceiveFailed,

    /// <summary>받은 바이트가 기대한 프레임 형식이 아닙니다.</summary>
    InvalidFrame,
}
