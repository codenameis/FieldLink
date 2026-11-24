namespace FieldLink.Transport.WebSocket;

/// <summary>WebSocket 메시지 프레임의 페이로드 유형입니다.</summary>
public enum WebSocketMessageKind
{
    /// <summary>UTF-8 텍스트 메시지입니다.</summary>
    Text,

    /// <summary>이진 바이트 메시지입니다.</summary>
    Binary,
}
