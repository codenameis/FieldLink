using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Http;

/// <summary>단일 <see cref="HttpClient"/>와 쿠키 컨테이너를 재사용해 HTTP/HTTPS 요청을 수행하는 전송 구현체입니다.</summary>
public sealed class HttpTransport : IHttpTransport
{
    private const string HttpSchemePrefix = "http://";
    private const string HttpsSchemePrefix = "https://";
    private const string ContentTypeHeaderName = "Content-Type";
    private const char PathSeparator = '/';

    private readonly HttpTransportOptions _options;
    private readonly HttpClient _client;
    private int _disposed;

    /// <summary>HTTP 전송 설정으로 새 인스턴스를 초기화합니다.</summary>
    /// <param name="options">호스트, 인증, TLS, 프록시, 제한 시간 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public HttpTransport(HttpTransportOptions options)
        : this(options, CreateHandler(options ?? throw new ArgumentNullException(nameof(options))), disposeHandler: true)
    {
    }

    internal HttpTransport(HttpTransportOptions options, HttpMessageHandler handler, bool disposeHandler)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _client = new HttpClient(handler, disposeHandler)
        {
            Timeout = _options.Timeout
        };
    }

    /// <inheritdoc />
    public async Task<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ThrowIfDisposed();

        using HttpRequestMessage message = BuildRequestMessage(request);
        return await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _client.Dispose();
    }

    private async Task<HttpTransportResponse> SendMessageAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            byte[] responseBytes = response.Content is not null
                ? await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                : Array.Empty<byte>();
            string? contentType = response.Content?.Headers.ContentType?.ToString();

            return new HttpTransportResponse((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBytes, contentType);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException($"HTTP 요청 제한 시간({_options.Timeout.TotalMilliseconds}ms)이 초과되었습니다.", exception);
            throw new TransportException(
                TransportFailure.ReceiveFailed,
                timeoutException.Message,
                isRequestSent: true,
                innerException: timeoutException);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "HTTP 전송이 닫혀 요청을 완료할 수 없습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(
                TransportFailure.ReceiveFailed,
                $"HTTP 요청 전송 중 오류가 발생했습니다. ({message.RequestUri})",
                isRequestSent: true,
                innerException: exception);
        }
    }

    private static HttpClientHandler CreateHandler(HttpTransportOptions options)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            UseProxy = options.UseProxy
        };

        if (!string.IsNullOrEmpty(options.Username))
        {
            handler.Credentials = new NetworkCredential(options.Username, options.Password ?? string.Empty);
        }

        if (options.AllowUntrustedServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }

        return handler;
    }

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
        string raw = baseUri.AbsoluteUri;
        return raw.EndsWith("/", StringComparison.Ordinal)
            ? baseUri
            : new Uri(raw + PathSeparator, UriKind.Absolute);
    }

    private static void ApplyHeaders(HttpRequestMessage message, IReadOnlyDictionary<string, string> headers)
    {
        foreach (KeyValuePair<string, string> pair in headers)
        {
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

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(HttpTransport));
        }
    }
}
