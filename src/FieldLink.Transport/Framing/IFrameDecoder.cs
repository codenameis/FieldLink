using System;

namespace FieldLink.Transport.Framing;

/// <summary>수신 버퍼에서 프로토콜 프레임 하나가 어디서 끝나는지 판정합니다.</summary>
/// <remarks>
/// <para>프레임 경계만 판정합니다. 내용의 의미(장치 오류 코드 등)는 프로토콜의 ResponseParser가 해석합니다.</para>
/// <para>상태를 갖지 않아야 합니다. 같은 입력에는 항상 같은 결과를 반환합니다.</para>
/// </remarks>
public interface IFrameDecoder
{
    /// <summary>받은 바이트의 앞부분에 완성된 프레임이 있으면 그 길이를 구합니다.</summary>
    /// <param name="received">지금까지 받은 바이트입니다. 항상 프레임의 시작부터 들어옵니다.</param>
    /// <param name="frameLength">완성된 프레임의 바이트 수입니다. false이면 0입니다.</param>
    /// <returns>프레임이 완성되었으면 true, 바이트가 더 필요하면 false입니다.</returns>
    /// <exception cref="TransportException">
    /// 앞부분이 이 프로토콜의 프레임일 수 없을 때 <see cref="TransportFailure.InvalidFrame"/>으로 던집니다.
    /// </exception>
    bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength);
}
