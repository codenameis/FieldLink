using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FieldLink.Transport.WebSocket;

/// <summary>WebSocket 연결과 메시지 프레이밍에 사용하는 불변 설정입니다.</summary>
public sealed class WebSocketTransportOptions
{
    private const string WebSocketScheme = "ws";
    private const string SecureWebSocketScheme = "wss";
    private const int DefaultMaximumMessageSize = 65536;
    private const int DefaultConnectTimeoutSeconds = 10;
    private const int DefaultKeepAliveIntervalSeconds = 30;

    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(DefaultConnectTimeoutSeconds);
    private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(DefaultKeepAliveIntervalSeconds);
    private static readonly IReadOnlyList<string> EmptySubProtocols = Array.Empty<string>();
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private readonly TimeSpan _connectTimeout = DefaultConnectTimeout;
    private readonly TimeSpan _keepAliveInterval = DefaultKeepAliveInterval;
    private readonly int _maximumMessageSize = DefaultMaximumMessageSize;
    private readonly IReadOnlyList<string> _subProtocols = EmptySubProtocols;
    private readonly IReadOnlyDictionary<string, string> _headers = EmptyHeaders;

    /// <summary>WebSocket 서버 URI로 전송 설정을 초기화합니다.</summary>
    /// <param name="serverUri">연결할 <c>ws://</c> 또는 <c>wss://</c> 절대 URI입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverUri"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="serverUri"/>가 <c>ws</c> 또는 <c>wss</c> 절대 URI가 아닐 때.</exception>
    public WebSocketTransportOptions(Uri serverUri)
    {
        if (serverUri is null)
        {
            throw new ArgumentNullException(nameof(serverUri));
        }

        if (!serverUri.IsAbsoluteUri
            || (!string.Equals(serverUri.Scheme, WebSocketScheme, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(serverUri.Scheme, SecureWebSocketScheme, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("서버 URI는 절대 ws 또는 wss 주소여야 합니다.", nameof(serverUri));
        }

        ServerUri = serverUri;
    }

    /// <summary>연결 대상 WebSocket 서버 URI입니다.</summary>
    public Uri ServerUri { get; }

    /// <summary>연결 핸드셰이크 제한 시간입니다. 기본값은 10초입니다.</summary>
    public TimeSpan ConnectTimeout
    {
        get => _connectTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "연결 제한 시간은 0보다 커야 합니다.");
            }

            _connectTimeout = value;
        }
    }

    /// <summary>프로토콜 수준 Keep-Alive 핑 주기입니다. 기본값은 30초입니다.</summary>
    public TimeSpan KeepAliveInterval
    {
        get => _keepAliveInterval;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Keep-Alive 간격은 0 이상이어야 합니다.");
            }

            _keepAliveInterval = value;
        }
    }

    /// <summary>요청할 WebSocket 하위 프로토콜 목록입니다.</summary>
    public IReadOnlyList<string> SubProtocols
    {
        get => _subProtocols;
        init
        {
            if (value is null)
            {
                _subProtocols = EmptySubProtocols;
                return;
            }

            var copy = new List<string>();
            foreach (string protocol in value)
            {
                if (string.IsNullOrWhiteSpace(protocol))
                {
                    throw new ArgumentException("WebSocket 하위 프로토콜 이름은 비어 있을 수 없습니다.", nameof(value));
                }

                copy.Add(protocol);
            }

            _subProtocols = Array.AsReadOnly(copy.ToArray());
        }
    }

    /// <summary>핸드셰이크 시 포함할 추가 HTTP 요청 헤더입니다.</summary>
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
                    throw new ArgumentException("WebSocket 요청 헤더 이름은 비어 있을 수 없습니다.", nameof(value));
                }

                if (pair.Value is null)
                {
                    throw new ArgumentException($"WebSocket 요청 헤더 '{pair.Key}'의 값은 null일 수 없습니다.", nameof(value));
                }

                copy[pair.Key] = pair.Value;
            }

            _headers = new ReadOnlyDictionary<string, string>(copy);
        }
    }

    /// <summary>시스템 프록시를 사용할지 여부입니다. 기본값은 false입니다.</summary>
    public bool UseProxy { get; init; }

    /// <summary>재조립할 단일 메시지의 최대 바이트 수입니다. 기본값은 65536입니다.</summary>
    public int MaximumMessageSize
    {
        get => _maximumMessageSize;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 메시지 크기는 1 이상이어야 합니다.");
            }

            _maximumMessageSize = value;
        }
    }
}
