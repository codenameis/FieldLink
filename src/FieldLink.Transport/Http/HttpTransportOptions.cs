using System;

namespace FieldLink.Transport.Http;

/// <summary>HTTP/HTTPS 전송 연결과 인증에 사용하는 불변 설정입니다.</summary>
public sealed class HttpTransportOptions
{
    private const int DefaultHttpPort = 80;
    private const int DefaultHttpsPort = 443;
    private const int MinimumPort = 1;
    private const int MaximumPort = 65535;
    private const int DefaultTimeoutSeconds = 10;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    private readonly TimeSpan _timeout = DefaultTimeout;

    /// <summary>기본 URI를 직접 지정하여 HTTP 전송 설정을 초기화합니다.</summary>
    /// <param name="baseUri">대상 장치의 HTTP 또는 HTTPS 기본 주소입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="baseUri"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseUri"/>가 절대 HTTP/HTTPS URI가 아닐 때.</exception>
    public HttpTransportOptions(Uri baseUri)
    {
        if (baseUri is null)
        {
            throw new ArgumentNullException(nameof(baseUri));
        }

        if (!baseUri.IsAbsoluteUri || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("기본 URI는 절대 http 또는 https 주소여야 합니다.", nameof(baseUri));
        }

        BaseUri = baseUri;
    }

    /// <summary>호스트와 포트, TLS 사용 여부로 HTTP 전송 설정을 초기화합니다.</summary>
    /// <param name="host">대상 호스트 이름 또는 IP 주소입니다.</param>
    /// <param name="port">대상 포트 번호입니다. 1~65535. 생략하면 HTTP는 80, HTTPS는 443을 사용합니다.</param>
    /// <param name="useHttps">HTTPS를 사용하면 true, HTTP를 사용하면 false입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="host"/>가 비어 있거나 공백일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/>가 유효 범위를 벗어났을 때.</exception>
    public HttpTransportOptions(string host, int? port = null, bool useHttps = false)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("호스트 이름 또는 IP 주소는 필수입니다.", nameof(host));
        }

        int resolvedPort = port ?? (useHttps ? DefaultHttpsPort : DefaultHttpPort);
        if (resolvedPort is < MinimumPort or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), resolvedPort, "포트 번호는 1에서 65535 사이여야 합니다.");
        }

        string scheme = useHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        BaseUri = new UriBuilder(scheme, host, resolvedPort).Uri;
    }

    /// <summary>대상 장치의 기본 HTTP/HTTPS URI입니다.</summary>
    public Uri BaseUri { get; }

    /// <summary>인증에 사용할 사용자 이름입니다. null이면 익명 요청을 보냅니다.</summary>
    public string? Username { get; init; }

    /// <summary>인증에 사용할 비밀번호입니다.</summary>
    public string? Password { get; init; }

    /// <summary>HTTP 요청 제한 시간입니다. 기본값은 10초입니다.</summary>
    /// <exception cref="ArgumentOutOfRangeException">0 이하의 시간을 지정했을 때.</exception>
    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "요청 제한 시간은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }

    /// <summary>시스템 프록시를 사용할지 여부입니다. 산업망 직접 통신을 위해 기본값은 false입니다.</summary>
    public bool UseProxy { get; init; }

    /// <summary>사설 서명 HTTPS 인증서를 허용할지 여부입니다. 기본값은 false입니다.</summary>
    public bool AllowUntrustedServerCertificate { get; init; }

    /// <summary>본문 전송 시 기본으로 사용할 Content-Type 헤더입니다.</summary>
    public string? DefaultContentType { get; init; }
}
