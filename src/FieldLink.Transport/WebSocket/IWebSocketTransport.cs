using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.WebSocket;

/// <summary>WebSocket 연결을 통해 텍스트 및 이진 메시지를 주고받는 전송 인터페이스입니다.</summary>
public interface IWebSocketTransport : IDisposable
{
    /// <summary>조회 시점의 연결 상태입니다.</summary>
    TransportState State { get; }

    /// <summary>WebSocket 서버에 연결을 수립합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>정상 종료 핸드셰이크를 수행하고 WebSocket 연결을 닫습니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>단일 WebSocket 메시지를 전송합니다.</summary>
    /// <param name="payload">전송할 바이트 배열입니다.</param>
    /// <param name="kind">텍스트 또는 이진 메시지 구분입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task SendAsync(byte[] payload, WebSocketMessageKind kind = WebSocketMessageKind.Text, CancellationToken cancellationToken = default);

    /// <summary>완성된 WebSocket 메시지 하나를 수신합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>재조립된 완성 메시지입니다.</returns>
    Task<WebSocketFrameMessage> ReceiveAsync(CancellationToken cancellationToken = default);
}
