using System;
using System.Net;

namespace FieldLink.Transport.Udp;

/// <summary>수신형 UDP 소켓에서 도착한 데이터그램과 송신자 엔드포인트 정보입니다.</summary>
public sealed class UdpDatagram
{
    private readonly byte[] _payload;

    /// <summary>페이로드와 원격 엔드포인트로 데이터그램을 초기화합니다.</summary>
    /// <param name="payload">수신된 바이트 배열입니다.</param>
    /// <param name="remoteEndPoint">데이터그램을 보낸 원격 IP 및 포트입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> 또는 <paramref name="remoteEndPoint"/>가 null일 때.</exception>
    public UdpDatagram(byte[] payload, IPEndPoint remoteEndPoint)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        _payload = (byte[])payload.Clone();
        RemoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
    }

    /// <summary>데이터그램을 보낸 원격 IP 및 포트입니다.</summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>수신된 페이로드 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>페이로드 바이트 배열입니다.</returns>
    public byte[] GetPayload() => (byte[])_payload.Clone();
}
