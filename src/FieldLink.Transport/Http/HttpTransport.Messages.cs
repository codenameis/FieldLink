using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace FieldLink.Transport.Http;

public sealed partial class HttpTransport
{
    private HttpRequestMessage BuildRequestMessage(HttpTransportRequest request)
    {
        Uri targetUri = ResolveTargetUri(request.PathOrUri);
        var message = new HttpRequestMessage(MapMethod(request.Method), targetUri);

        try
        {
            PopulateContentAndHeaders(message, request);
            return message;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }

    private void PopulateContentAndHeaders(HttpRequestMessage message, HttpTransportRequest request)
    {
        byte[]? bodyBytes = request.GetBodyBytes();
        if (bodyBytes is not null)
        {
            var content = new ByteArrayContent(bodyBytes);
            string? effectiveContentType = request.ContentType ?? _options.DefaultContentType;
            if (!string.IsNullOrWhiteSpace(effectiveContentType))
            {
                TransportHeaderValidation.ThrowIfContainsControlCharacters(
                    effectiveContentType,
                    nameof(request),
                    "Content-Type 헤더 값에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(effectiveContentType);
            }

            message.Content = content;
        }

        ApplyHeaders(message, request.Headers);
    }

    private Uri ResolveTargetUri(string pathOrUri)
    {
        if (pathOrUri.StartsWith(HttpSchemePrefix, StringComparison.OrdinalIgnoreCase)
            || pathOrUri.StartsWith(HttpsSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(pathOrUri, UriKind.Absolute);
        }

        string trimmed = pathOrUri.TrimStart(PathSeparator);
        Uri baseUri = EnsureTrailingSlash(_options.BaseUri);
        return new Uri(baseUri, trimmed);
    }

    private static Uri EnsureTrailingSlash(Uri baseUri)
    {
        if (baseUri.AbsolutePath.EndsWith(PathSeparatorText, StringComparison.Ordinal))
        {
            return baseUri;
        }

        var builder = new UriBuilder(baseUri);
        builder.Path += PathSeparator;
        return builder.Uri;
    }

    private static void ApplyHeaders(HttpRequestMessage message, IReadOnlyDictionary<string, string> headers)
    {
        foreach (KeyValuePair<string, string> pair in headers)
        {
            TransportHeaderValidation.ThrowIfContainsControlCharacters(
                pair.Key,
                nameof(headers),
                "HTTP 헤더 이름에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");
            TransportHeaderValidation.ThrowIfContainsControlCharacters(
                pair.Value,
                nameof(headers),
                $"HTTP 헤더 '{pair.Key}'의 값에는 제어 문자(\\r, \\n, \\0)를 포함할 수 없습니다.");

            if (string.Equals(pair.Key, ContentTypeHeaderName, StringComparison.OrdinalIgnoreCase))
            {
                if (message.Content is not null)
                {
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(pair.Value);
                }

                continue;
            }

            if (message.Headers.TryAddWithoutValidation(pair.Key, pair.Value))
            {
                continue;
            }

            message.Content?.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }
    }

    private static HttpMethod MapMethod(HttpMethodKind kind) =>
        kind switch
        {
            HttpMethodKind.Get => HttpMethod.Get,
            HttpMethodKind.Post => HttpMethod.Post,
            HttpMethodKind.Put => HttpMethod.Put,
            HttpMethodKind.Delete => HttpMethod.Delete,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 HTTP 메서드입니다.")
        };
}
