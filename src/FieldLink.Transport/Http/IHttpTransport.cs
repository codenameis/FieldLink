using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Http;

/// <summary>HTTP/HTTPS 요청을 보내고 원시 바이트 응답을 받는 전송 인터페이스입니다.</summary>
public interface IHttpTransport : IDisposable
{
    /// <summary>HTTP 요청을 전송하고 원시 응답을 반환합니다.</summary>
    /// <param name="request">전송할 HTTP 요청 정보입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>상태 코드와 원시 본문 바이트를 담은 응답입니다.</returns>
    /// <exception cref="TransportException">연결·송수신 실패 또는 제한 시간 초과 시.</exception>
    Task<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default);
}
