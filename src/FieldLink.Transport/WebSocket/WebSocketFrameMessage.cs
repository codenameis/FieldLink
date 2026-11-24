using System;

namespace FieldLink.Transport.WebSocket;

/// <summary>WebSocket에서 수신한 단일 완성 메시지입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class WebSocketFrameMessage
{
    private readonly byte[] _payload;

    /// <summary>메시지 유형과 페이로드 바이트로 메시지 객체를 초기화합니다.</summary>
    /// <param name="kind">텍스트 또는 이진 메시지 구분입니다.</param>
    /// <param name="payload">수신된 완성 메시지 바이트입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/>가 null일 때.</exception>
    public WebSocketFrameMessage(WebSocketMessageKind kind, byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        Kind = kind;
        _payload = (byte[])payload.Clone();
    }

    /// <summary>텍스트 또는 이진 메시지 구분입니다.</summary>
    public WebSocketMessageKind Kind { get; }

    /// <summary>메시지 페이로드 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>페이로드 바이트 배열입니다.</returns>
    public byte[] GetPayload() => (byte[])_payload.Clone();
}
