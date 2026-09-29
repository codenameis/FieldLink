using System;

namespace FieldLink.Communication.Framing
{
    /// <summary>누적된 TCP 바이트의 첫 프레임 길이를 판정합니다. 소켓이나 프로토콜 세션을 소유하지 않습니다.</summary>
    public interface IFrameBoundary
    {
        /// <summary>
        /// 프레임 전체 길이를 알면 양수를, 정보가 더 필요하면 null을 반환합니다.
        /// 길이는 아직 수신되지 않은 본문을 포함할 수 있습니다. 입력을 변경하거나 보관하지 마세요.
        /// 잘못된 프레임은 System.IO.InvalidDataException으로 거부합니다.
        /// </summary>
        int? GetFrameLength(ArraySegment<byte> bufferedData);
    }
}
