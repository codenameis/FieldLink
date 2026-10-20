using System;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;

namespace FieldLink.Transport;

/// <summary><see cref="ITransportSession"/>의 자주 쓰는 조합 동작입니다.</summary>
public static class TransportSessionExtensions
{
    /// <summary>요청 하나를 보내고 응답 프레임 하나를 받습니다.</summary>
    /// <param name="session">독점 송수신 구간입니다.</param>
    /// <param name="request">보낼 요청 프레임입니다.</param>
    /// <param name="responseDecoder">응답 프레임 경계 규칙입니다.</param>
    /// <returns>응답 프레임입니다.</returns>
    public static async Task<byte[]> ExchangeAsync(this ITransportSession session, byte[] request, IFrameDecoder responseDecoder)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        await session.SendAsync(request).ConfigureAwait(false);
        return await session.ReceiveAsync(responseDecoder).ConfigureAwait(false);
    }
}
