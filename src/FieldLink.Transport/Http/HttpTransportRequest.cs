using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FieldLink.Transport.Http;

/// <summary>HTTP 전송 계층으로 보낼 불변 요청 설명입니다.</summary>
public sealed class HttpTransportRequest
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly byte[]? _bodyBytes;
    private readonly IReadOnlyDictionary<string, string> _headers = EmptyHeaders;

    /// <summary>메서드와 경로, 선택적 본문으로 HTTP 요청을 초기화합니다.</summary>
    /// <param name="method">HTTP 요청 메서드입니다.</param>
    /// <param name="pathOrUri">기본 URI 기준 상대 경로 또는 절대 URL입니다.</param>
    /// <param name="bodyBytes">요청 본문 바이트입니다. 없으면 null입니다.</param>
    /// <param name="contentType">본문에 적용할 Content-Type입니다. null이면 옵션의 기본값을 사용합니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pathOrUri"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="pathOrUri"/>가 비어 있거나 공백일 때.</exception>
    public HttpTransportRequest(
        HttpMethodKind method,
        string pathOrUri,
        byte[]? bodyBytes = null,
        string? contentType = null)
    {
        if (pathOrUri is null)
        {
            throw new ArgumentNullException(nameof(pathOrUri));
        }

        if (string.IsNullOrWhiteSpace(pathOrUri))
        {
            throw new ArgumentException("요청 경로 또는 URL은 비어 있을 수 없습니다.", nameof(pathOrUri));
        }

        TransportHeaderValidation.ThrowIfContainsControlCharacters(
            contentType,
            nameof(contentType),
            "Content-Type 헤더 값에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");

        Method = method;
        PathOrUri = pathOrUri;
        _bodyBytes = bodyBytes is not null ? (byte[])bodyBytes.Clone() : null;
        ContentType = contentType;
    }

    /// <summary>HTTP 요청 메서드입니다.</summary>
    public HttpMethodKind Method { get; }

    /// <summary>상대 경로 또는 절대 URL 문자열입니다.</summary>
    public string PathOrUri { get; }

    /// <summary>본문에 지정한 Content-Type입니다.</summary>
    public string? ContentType { get; }

    /// <summary>요청에 추가로 포함할 사용자 지정 HTTP 헤더입니다.</summary>
    public IReadOnlyDictionary<string, string> Headers
    {
        get => _headers;
        init
        {
            if (value is null)
            {
                _headers = EmptyHeaders;
                return;
            }

            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in value)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException("HTTP 헤더 이름은 비어 있을 수 없습니다.", nameof(value));
                }

                if (pair.Value is null)
                {
                    throw new ArgumentException($"HTTP 헤더 '{pair.Key}'의 값은 null일 수 없습니다.", nameof(value));
                }

                TransportHeaderValidation.ThrowIfContainsControlCharacters(
                    pair.Key,
                    nameof(value),
                    "HTTP 헤더 이름에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");
                TransportHeaderValidation.ThrowIfContainsControlCharacters(
                    pair.Value,
                    nameof(value),
                    $"HTTP 헤더 '{pair.Key}'의 값에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");

                copy[pair.Key] = pair.Value;
            }

            _headers = new ReadOnlyDictionary<string, string>(copy);
        }
    }

    /// <summary>요청 본문 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>본문이 있으면 복사된 바이트 배열, 없으면 null입니다.</returns>
    public byte[]? GetBodyBytes() => _bodyBytes is not null ? (byte[])_bodyBytes.Clone() : null;
}
