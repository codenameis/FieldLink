using System;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication
{
    /// <summary>프레임 내부에서 허용하는 최대 문자 간 무수신 간격을 제공합니다.</summary>
    /// <remarks>물리 수신 간격을 제공하는 ITimedSerialPortChannel에서만 검사합니다. 송신 전 간격과는 별개입니다.</remarks>
    public interface ISerialReceiveTiming : IFrameBoundary
    {
        /// <summary>baud와 시작·데이터·패리티·정지 비트를 합한 문자 길이로 최대 간격을 계산합니다.</summary>
        TimeSpan GetMaximumInterCharacterInterval(int baudRate, double bitsPerCharacter);
    }
}
