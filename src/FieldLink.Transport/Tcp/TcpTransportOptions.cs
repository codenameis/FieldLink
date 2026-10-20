using System;

namespace FieldLink.Transport.Tcp;

/// <summary>TCP 전송의 연결 및 소켓 설정입니다.</summary>
public sealed class TcpTransportOptions
{
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(3);
    private const int DefaultBufferSize = 8192;

    /// <summary>호스트와 포트로 TCP 전송 설정을 초기화합니다.</summary>
    /// <param name="host">대상 호스트 이름 또는 IP 주소입니다.</param>
    /// <param name="port">대상 TCP 포트 번호입니다. 1~65535.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="host"/>가 비어 있거나 공백일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/>가 유효 범위를 벗어났을 때.</exception>
    public TcpTransportOptions(string host, int port)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("호스트 이름 또는 IP 주소는 필수입니다.", nameof(host));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "포트 번호는 1에서 65535 사이여야 합니다.");
        }

        Host = host;
        Port = port;
        ConnectTimeout = DefaultConnectTimeout;
        ReceiveBufferSize = DefaultBufferSize;
        SendBufferSize = DefaultBufferSize;
        NoDelay = true;
    }

    /// <summary>대상 호스트 이름 또는 IP 주소입니다.</summary>
    public string Host { get; }

    /// <summary>대상 TCP 포트 번호입니다.</summary>
    public int Port { get; }

    /// <summary>연결 시도 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan ConnectTimeout { get; init; }

    /// <summary>소켓 수신 버퍼 크기입니다. 바이트 단위.</summary>
    public int ReceiveBufferSize { get; init; }

    /// <summary>소켓 송신 버퍼 크기입니다. 바이트 단위.</summary>
    public int SendBufferSize { get; init; }

    /// <summary>Nagle 알고리즘을 비활성화할지 여부입니다. 기본값은 true(지연 없음)입니다.</summary>
    public bool NoDelay { get; init; }
}
