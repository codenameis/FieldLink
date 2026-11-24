using System;
using System.Net;

namespace FieldLink.Transport.Udp;

/// <summary>수신형 UDP 리스너 전송의 로컬 바인딩 설정입니다.</summary>
public sealed class UdpListenerTransportOptions
{
    private const int MinimumLocalPort = 0;
    private const int MaximumPort = 65535;

    /// <summary>로컬 바인딩 포트와 IP 주소로 리스너 설정을 초기화합니다.</summary>
    /// <param name="localPort">수신 대기할 로컬 UDP 포트입니다. 0을 지정하면 운영체제가 임시 포트를 할당합니다.</param>
    /// <param name="localAddress">바인딩할 로컬 IP 주소입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="localPort"/>가 0~65535 범위를 벗어났을 때.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="localAddress"/>가 null일 때.</exception>
    public UdpListenerTransportOptions(int localPort, IPAddress localAddress)
    {
        if (localPort is < MinimumLocalPort or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(nameof(localPort), localPort, "로컬 포트 번호는 0에서 65535 사이여야 합니다.");
        }

        LocalPort = localPort;
        LocalAddress = localAddress ?? throw new ArgumentNullException(nameof(localAddress));
    }

    /// <summary>지정된 로컬 포트에 <see cref="IPAddress.Any"/>로 바인딩하는 설정을 초기화합니다.</summary>
    /// <param name="localPort">수신 대기할 로컬 UDP 포트입니다. 0~65535.</param>
    public UdpListenerTransportOptions(int localPort)
        : this(localPort, IPAddress.Any)
    {
    }

    /// <summary>수신 대기할 로컬 UDP 포트입니다.</summary>
    public int LocalPort { get; }

    /// <summary>바인딩할 로컬 IP 주소입니다.</summary>
    public IPAddress LocalAddress { get; }
}
