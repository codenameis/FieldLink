using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport.Http;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB Robot Web Services(RWS) HTTP/HTTPS REST API 클라이언트입니다.</summary>
public sealed partial class AbbRwsClient : IRobotClient
{
    private const int HttpStatusUnauthorized = 401;
    private const string UrlPrefix = "url=";

    private readonly IHttpTransport _transport;
    private readonly AbbRwsOptions _options;
    private int _isOpen;
    private int _isDisposed;

    /// <summary>설정 정보로부터 내부 HTTP 전송을 생성해 ABB RWS 클라이언트를 초기화합니다.</summary>
    /// <param name="options">ABB RWS 연결 및 인증 설정입니다.</param>
    public AbbRwsClient(AbbRwsOptions options)
        : this(new HttpTransport((options ?? throw new ArgumentNullException(nameof(options))).ToHttpTransportOptions()), options)
    {
    }

    /// <summary>사용자 정의 HTTP 전송을 주입받아 ABB RWS 클라이언트를 초기화합니다.</summary>
    /// <param name="transport">HTTP 요청을 수행할 전송 구현체입니다.</param>
    /// <param name="options">디코딩 및 언어 설정입니다. null이면 기본 설정을 사용합니다.</param>
    public AbbRwsClient(IHttpTransport transport, AbbRwsOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new AbbRwsOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => Volatile.Read(ref _isDisposed) == 0 && Volatile.Read(ref _isOpen) != 0;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _isOpen, 1, 0) != 0)
        {
            throw new InvalidOperationException("ABB RWS 클라이언트가 이미 열려 있습니다.");
        }

        if (Volatile.Read(ref _isDisposed) != 0)
        {
            Volatile.Write(ref _isOpen, 0);
            throw new ObjectDisposedException(nameof(AbbRwsClient));
        }

        return Task.FromResult(RobotResult.Success());
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _isOpen, 0);
        return Task.FromResult(RobotResult.Success());
    }

    /// <inheritdoc />
    public async Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<string> stringResult = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
        return stringResult.IsSuccess
            ? RobotResult.Success(Encoding.UTF8.GetBytes(stringResult.Value))
            : RobotResult.Failure<byte[]>(stringResult.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        AbbRwsAddress parsed = AbbRwsAddress.Parse(address);

        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(parsed, _options.EventLogLanguage),
            body => AbbRwsResponseParser.ParseShortcutAsString(parsed, body),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소는 비어 있을 수 없습니다.", nameof(address));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ThrowIfDisposed();
        EnsureOpen();

        if (!address.TrimStart().StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, "ABB RWS 쓰기는 'url=' 접두사 경로만 지원합니다.")));
        }

        AbbRwsAddress parsed = AbbRwsAddress.Parse(address);
        if (parsed.RawUrl is not string rawUrl)
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, "ABB RWS 쓰기는 'url=' 접두사 경로만 지원합니다.")));
        }

        return HttpPostBytesAsync(rawUrl, data, customHeaders: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteRawAsync(address, Encoding.UTF8.GetBytes(value), cancellationToken);
    }

    /// <summary>원시 URL에 대해 HTTP GET 요청을 실행하고 응답 본문 문자열을 반환합니다.</summary>
    public Task<RobotResult<string>> HttpGetAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(AbbRwsRequestBuilder.BuildRawGetRequest(rawUrl), body => body, cancellationToken);
    }

    /// <summary>원시 URL에 대해 HTTP POST 요청을 실행하고 응답 본문 문자열을 반환합니다.</summary>
    public Task<RobotResult<string>> HttpPostAsync(
        string rawUrl,
        string body,
        IReadOnlyDictionary<string, string>? customHeaders = null,
        CancellationToken cancellationToken = default)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        ThrowIfDisposed();
        EnsureOpen();
        HttpTransportRequest request = AbbRwsRequestBuilder.BuildRawPostRequest(
            rawUrl,
            Encoding.UTF8.GetBytes(body),
            _options.DefaultContentType,
            customHeaders);

        return ExecuteRequestAsync(request, responseBody => responseBody, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        _transport.Dispose();
    }

    private Task<RobotResult> HttpPostBytesAsync(
        string rawUrl,
        byte[] data,
        IReadOnlyDictionary<string, string>? customHeaders,
        CancellationToken cancellationToken)
    {
        HttpTransportRequest request = AbbRwsRequestBuilder.BuildRawPostRequest(
            rawUrl,
            data,
            _options.DefaultContentType,
            customHeaders);

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                HttpTransportResponse response = await _transport.SendAsync(request, token).ConfigureAwait(false);
                EnsureSuccessStatusCode(response);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult<T>> ExecuteRequestAsync<T>(
        HttpTransportRequest request,
        Func<string, T> parser,
        CancellationToken cancellationToken) =>
        RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                HttpTransportResponse response = await _transport.SendAsync(request, token).ConfigureAwait(false);
                EnsureSuccessStatusCode(response);
                string body = AbbRwsResponseParser.DecodeBody(response.GetBodyBytes(), _options.UseIso88591);
                return parser(body);
            },
            _options.Timeout,
            cancellationToken);

    private static void EnsureSuccessStatusCode(HttpTransportResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        byte[] rawBody = response.GetBodyBytes();
        if (response.StatusCode == HttpStatusUnauthorized)
        {
            throw new RobotException(
                RobotErrorKind.AuthenticationFailed,
                "ABB RWS HTTP 인증에 실패했습니다. (401 Unauthorized)",
                rawBody)
            {
                ProtocolErrorCode = HttpStatusUnauthorized
            };
        }

        throw new RobotDeviceException(
            response.StatusCode,
            $"ABB RWS HTTP 요청이 실패했습니다. ({response.StatusCode} {response.ReasonPhrase})",
            rawBody);
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("ABB RWS 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(nameof(AbbRwsClient));
        }
    }
}
