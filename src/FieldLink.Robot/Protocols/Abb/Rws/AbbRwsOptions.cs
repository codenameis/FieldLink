using System;
using FieldLink.Transport.Http;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB Robot Web Services(RWS) 클라이언트 연결과 디코딩에 사용하는 불변 설정입니다.</summary>
public sealed class AbbRwsOptions
{
    private const int DefaultHttpPort = 80;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly string _host = "127.0.0.1";
    private readonly int _port = DefaultHttpPort;
    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly string _eventLogLanguage = "en";

    /// <summary>기본 설정값으로 ABB RWS 옵션을 초기화합니다.</summary>
    public AbbRwsOptions()
    {
    }

    /// <summary>호스트와 포트, TLS 사용 여부로 ABB RWS 옵션을 초기화합니다.</summary>
    /// <param name="host">ABB 컨트롤러 호스트 이름 또는 IP 주소입니다.</param>
    /// <param name="port">HTTP 또는 HTTPS 포트 번호입니다. 1~65535.</param>
    /// <param name="useHttps">HTTPS를 사용하면 true, HTTP를 사용하면 false입니다.</param>
    public AbbRwsOptions(string host, int port = DefaultHttpPort, bool useHttps = false)
    {
        Host = host;
        Port = port;
        UseHttps = useHttps;
    }

    /// <summary>대상 ABB 컨트롤러 호스트 이름 또는 IP 주소입니다.</summary>
    public string Host
    {
        get => _host;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("호스트 이름은 비어 있을 수 없습니다.", nameof(value));
            }

            _host = value;
        }
    }

    /// <summary>대상 HTTP 또는 HTTPS 포트 번호입니다.</summary>
    public int Port
    {
        get => _port;
        init
        {
            if (value is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "포트 번호는 1에서 65535 사이여야 합니다.");
            }

            _port = value;
        }
    }

    /// <summary>HTTPS 연결 사용 여부입니다. 기본값은 false입니다.</summary>
    public bool UseHttps { get; init; }

    /// <summary>HTTP 인증 사용자 이름입니다. null이면 기본 익명 요청을 사용합니다.</summary>
    public string? Username { get; init; }

    /// <summary>HTTP 인증 비밀번호입니다.</summary>
    public string? Password { get; init; }

    /// <summary>단일 요청 제한 시간입니다. 기본값은 10초입니다.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "제한 시간은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }

    /// <summary>시스템 프록시 사용 여부입니다. 기본값은 false입니다.</summary>
    public bool UseProxy { get; init; }

    /// <summary>사설 서명 HTTPS 인증서 허용 여부입니다. 기본값은 false입니다.</summary>
    public bool AllowUntrustedServerCertificate { get; init; }

    /// <summary>비표준 Content-Type 회피를 위해 응답을 ISO-8859-1로 강제 디코딩할지 여부입니다.</summary>
    public bool UseIso88591 { get; init; }

    /// <summary>POST 요청 본문에 사용할 기본 Content-Type입니다.</summary>
    public string? DefaultContentType { get; init; }

    /// <summary>이벤트 로그 조회 시 전달할 언어 쿼리 파라미터입니다. 기본값은 <c>en</c>입니다.</summary>
    public string EventLogLanguage
    {
        get => _eventLogLanguage;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("언어 코드는 비어 있을 수 없습니다.", nameof(value));
            }

            _eventLogLanguage = value;
        }
    }

    internal HttpTransportOptions ToHttpTransportOptions() =>
        new(_host, _port, UseHttps)
        {
            Username = Username,
            Password = Password,
            Timeout = _timeout,
            UseProxy = UseProxy,
            AllowUntrustedServerCertificate = AllowUntrustedServerCertificate,
            DefaultContentType = DefaultContentType
        };
}
