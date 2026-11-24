using System;

namespace FieldLink.Transport.Udp;

/// <summary>UDP 전송의 대상 호스트와 포트 설정입니다.</summary>
public sealed class UdpTransportOptions
{
    private const int MinimumPort = 1;
    private const int MaximumPort = 65535;
    private const int DefaultReceiveTimeoutSeconds = 3;

    private static readonly TimeSpan DefaultReceiveTimeout = TimeSpan.FromSeconds(DefaultReceiveTimeoutSeconds);

    private readonly TimeSpan _receiveTimeout = DefaultReceiveTimeout;

    /// <summary>호스트와 포트로 UDP 전송 설정을 초기화합니다.</summary>
    /// <param name="host">대상 호스트 이름 또는 IP 주소입니다.</param>
    /// <param name="port">대상 UDP 포트 번호입니다. 1~65535.</param>
    /// <param name="localPort">바인딩할 로컬 UDP 포트 번호입니다. 지정하지 않으면 임의 포트가 할당됩니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="host"/>가 비어 있거나 공백일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">포트 번호가 유효 범위를 벗어났을 때.</exception>
    public UdpTransportOptions(string host, int port, int? localPort = null)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("호스트 이름 또는 IP 주소는 필수입니다.", nameof(host));
        }

        if (port is < MinimumPort or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "포트 번호는 1에서 65535 사이여야 합니다.");
        }

        if (localPort.HasValue && localPort.Value is < MinimumPort or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), localPort.Value, "로컬 포트 번호는 1에서 65535 사이여야 합니다.");
        }

        Host = host;
        Port = port;
        LocalPort = localPort;
    }

    /// <summary>대상 호스트 이름 또는 IP 주소입니다.</summary>
    public string Host { get; }

    /// <summary>대상 UDP 포트 번호입니다.</summary>
    public int Port { get; }

    /// <summary>로컬 바인딩 포트 번호입니다. null이면 임의 할당 포트를 사용합니다.</summary>
    public int? LocalPort { get; }

    /// <summary>데이터그램 수신 제한 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan ReceiveTimeout
    {
        get => _receiveTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "수신 제한 시간은 0보다 커야 합니다.");
            }

            _receiveTimeout = value;
        }
    }
}
