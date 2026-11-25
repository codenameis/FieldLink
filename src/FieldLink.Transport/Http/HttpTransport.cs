using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Http;

/// <summary>단일 <see cref="HttpClient"/>와 쿠키 컨테이너를 재사용해 HTTP/HTTPS 요청을 수행하는 전송 구현체입니다.</summary>
public sealed partial class HttpTransport : IHttpTransport
{
    private const string HttpSchemePrefix = "http://";
    private const string HttpsSchemePrefix = "https://";
    private const string ContentTypeHeaderName = "Content-Type";
    private const string BasicAuthenticationScheme = "Basic";
    private const string DigestAuthenticationScheme = "Digest";
    private const int ResponseReadBufferSize = 8192;
    private const char PathSeparator = '/';
    private const string PathSeparatorText = "/";

    private readonly HttpTransportOptions _options;
    private readonly HttpClient _client;
    private int _disposed;

    /// <summary>HTTP 전송 설정으로 새 인스턴스를 초기화합니다.</summary>
    /// <param name="options">호스트, 인증, TLS, 프록시, 제한 시간 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public HttpTransport(HttpTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        HttpClientHandler handler = CreateHandler(options);
        try
        {
            _client = CreateClient(options, handler, disposeHandler: true);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    internal HttpTransport(HttpTransportOptions options, HttpMessageHandler handler, bool disposeHandler)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _client = CreateClient(options, handler, disposeHandler);
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

    private static HttpClient CreateClient(HttpTransportOptions options, HttpMessageHandler handler, bool disposeHandler) =>
        new(handler, disposeHandler)
        {
            Timeout = options.Timeout
        };

    internal static HttpClientHandler CreateHandler(HttpTransportOptions options)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            UseProxy = options.UseProxy,
            AllowAutoRedirect = false
        };

        if (!string.IsNullOrEmpty(options.Username))
        {
            Uri originUri = new UriBuilder(options.BaseUri.Scheme, options.BaseUri.Host, options.BaseUri.Port).Uri;
            var credential = new NetworkCredential(options.Username, options.Password ?? string.Empty);
            var credentialCache = new CredentialCache();
            credentialCache.Add(originUri, BasicAuthenticationScheme, credential);
            credentialCache.Add(originUri, DigestAuthenticationScheme, credential);
            handler.Credentials = credentialCache;
        }

        if (options.AllowUntrustedServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }

        return handler;
    }

    private async Task<HttpTransportResponse> SendMessageAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = new(_options.Timeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            using HttpResponseMessage response = await _client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token)
                .ConfigureAwait(false);

            byte[] responseBytes = await ReadBoundedResponseBytesAsync(response.Content, linkedSource.Token)
                .ConfigureAwait(false);
            string? contentType = response.Content?.Headers.ContentType?.ToString();

            return new HttpTransportResponse((int)response.StatusCode, response.ReasonPhrase ?? string.Empty, responseBytes, contentType);
        }
        catch (TransportException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (Volatile.Read(ref _disposed) != 0 && !cancellationToken.IsCancellationRequested)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "HTTP 전송이 닫혀 요청을 완료할 수 없습니다.",
                isRequestSent: true,
                innerException: exception);
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
        catch (Exception exception) when (Volatile.Read(ref _disposed) != 0)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "HTTP 전송이 닫혀 요청을 완료할 수 없습니다.",
                isRequestSent: true,
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

    private async Task<byte[]> ReadBoundedResponseBytesAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return Array.Empty<byte>();
        }

        int maxBytes = _options.MaximumResponseBodyBytes;
        long? declaredLength = content.Headers.ContentLength;
        if (declaredLength.HasValue && declaredLength.Value > maxBytes)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"HTTP 응답 본문 길이({declaredLength.Value} 바이트)가 최대 허용 크기({maxBytes} 바이트)를 초과했습니다.",
                isRequestSent: true);
        }

        using Stream responseStream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using var bufferStream = new MemoryStream();
        byte[] readBuffer = new byte[Math.Min(ResponseReadBufferSize, maxBytes)];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bytesRead = await ReadChunkWithCancellationAsync(responseStream, readBuffer, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead <= 0)
            {
                break;
            }

            if (bufferStream.Length + bytesRead > maxBytes)
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    $"HTTP 응답 본문 크기가 최대 허용 크기({maxBytes} 바이트)를 초과했습니다.",
                    isRequestSent: true);
            }

            bufferStream.Write(readBuffer, 0, bytesRead);
        }

        return bufferStream.ToArray();
    }

    private static Task<int> ReadChunkWithCancellationAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        Task<int> readTask = stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        return TransportTaskWait.WaitWithCancellationAsync(readTask, cancellationToken);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(HttpTransport));
        }
    }
}
