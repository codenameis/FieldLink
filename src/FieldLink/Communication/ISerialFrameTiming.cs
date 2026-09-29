using System;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication
{
    /// <summary>시리얼 프레임 경계가 추가로 제공하는 최소 무통신 간격입니다. 포트 설정별 규칙은 해당 프로토콜이 결정합니다.</summary>
    /// <remarks>이를 구현한 IFrameBoundary로 교환할 때 전송은 포트 전체의 마지막 활동 이후 간격을 지킵니다.</remarks>
    public interface ISerialFrameTiming : IFrameBoundary
    {
        /// <summary>실제 baud와 시작·데이터·패리티·정지 비트를 합한 문자 길이에 필요한 간격을 반환합니다.</summary>
        TimeSpan GetMinimumSilentInterval(int baudRate, double bitsPerCharacter);
    }
}
