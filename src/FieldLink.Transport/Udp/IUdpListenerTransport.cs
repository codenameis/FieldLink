using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

/// <summary>로컬 UDP 포트에 바인딩해 임의 원격 장치의 데이터그램을 수신하고 응답을 보내는 전송 인터페이스입니다.</summary>
public interface IUdpListenerTransport : IDisposable
{
    /// <summary>조회 시점의 연결 상태입니다.</summary>
    TransportState State { get; }

    /// <summary>현재 바인딩된 실제 로컬 UDP 포트입니다. 열려 있지 않으면 0입니다.</summary>
    int BoundPort { get; }

    /// <summary>로컬 UDP 포트를 열어 수신 대기를 시작합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>UDP 리스너 소켓을 닫습니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>원격 장치로부터 다음 UDP 데이터그램 하나를 수신합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>페이로드와 송신자 엔드포인트가 담긴 데이터그램입니다.</returns>
    Task<UdpDatagram> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>지정된 원격 엔드포인트로 UDP 데이터그램을 송신합니다.</summary>
    /// <param name="payload">전송할 바이트 배열입니다.</param>
    /// <param name="remoteEndPoint">수신 대상 원격 엔드포인트입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task SendToAsync(byte[] payload, IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default);
}
