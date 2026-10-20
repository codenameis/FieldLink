using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;

namespace FieldLink.Transport;

/// <summary><see cref="ITransport.RunExclusiveAsync{TResult}"/> 안에서만 유효한 독점 송수신 구간입니다.</summary>
public interface ITransportSession
{
    /// <summary>연결이 새로 수립될 때마다 증가하는 번호입니다.</summary>
    /// <remarks>연결에 묶인 프로토콜 상태(FINS 노드 주소, CIP 세션 핸들 등)가 현재 연결의 것인지 확인할 때 사용합니다.</remarks>
    long ConnectionId { get; }

    /// <summary>이 구간 전체에 적용되는 취소 또는 제한 시간 토큰입니다.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>프레임 하나를 끝까지 보냅니다.</summary>
    /// <param name="frame">보낼 바이트입니다. 반환된 Task가 끝날 때까지 수정하지 마세요.</param>
    /// <exception cref="TransportException">보내지 못했을 때.</exception>
    Task SendAsync(byte[] frame);

    /// <summary>완성된 프레임 하나를 받을 때까지 기다립니다.</summary>
    /// <param name="decoder">프레임 경계를 판정하는 규칙입니다.</param>
    /// <returns>완성된 프레임 하나입니다. 남은 바이트는 다음 수신에 사용됩니다.</returns>
    /// <exception cref="TransportException">연결이 끊기거나 프레임 형식이 잘못되었을 때.</exception>
    Task<byte[]> ReceiveAsync(IFrameDecoder decoder);
}
