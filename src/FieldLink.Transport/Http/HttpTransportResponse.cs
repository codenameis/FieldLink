using System;

namespace FieldLink.Transport.Http;

/// <summary>HTTP 전송에서 수신한 원시 응답입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class HttpTransportResponse
{
    private const int MinSuccessStatusCode = 200;
    private const int MaxSuccessStatusCode = 299;

    private readonly byte[] _bodyBytes;

    /// <summary>HTTP 상태 코드와 원시 응답 바이트로 응답 객체를 초기화합니다.</summary>
    /// <param name="statusCode">HTTP 상태 코드입니다.</param>
    /// <param name="reasonPhrase">상태 설명 문자열입니다.</param>
    /// <param name="bodyBytes">수신한 원시 본문 바이트입니다.</param>
    /// <param name="contentType">응답 Content-Type 헤더 값입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bodyBytes"/>가 null일 때.</exception>
    public HttpTransportResponse(int statusCode, string reasonPhrase, byte[] bodyBytes, string? contentType = null)
    {
        if (bodyBytes is null)
        {
            throw new ArgumentNullException(nameof(bodyBytes));
        }

        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase ?? string.Empty;
        _bodyBytes = (byte[])bodyBytes.Clone();
        ContentType = contentType;
    }

    /// <summary>HTTP 응답 상태 코드입니다.</summary>
    public int StatusCode { get; }

    /// <summary>HTTP 상태 사유 구문입니다.</summary>
    public string ReasonPhrase { get; }

    /// <summary>응답 Content-Type 헤더 문자열입니다.</summary>
    public string? ContentType { get; }

    /// <summary>상태 코드가 200~299 범위의 성공인지 여부입니다.</summary>
    public bool IsSuccessStatusCode => StatusCode is >= MinSuccessStatusCode and <= MaxSuccessStatusCode;

    /// <summary>수신한 원시 본문 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>응답 본문 바이트 배열입니다.</returns>
    public byte[] GetBodyBytes() => (byte[])_bodyBytes.Clone();
}
