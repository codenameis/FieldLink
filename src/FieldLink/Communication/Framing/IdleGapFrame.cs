using System;

namespace FieldLink.Communication.Framing
{
    /// <summary>SerialClient에서 수신이 잠시 멈춘 시점을 경계로 사용합니다. 명확한 길이나 종단자가 없을 때만 사용하세요.</summary>
    public sealed class IdleGapFrame : IFrameBoundary
    {
        /// <summary>최소 수신 길이와 마지막 바이트 이후의 무수신 시간을 지정합니다.</summary>
        public IdleGapFrame(TimeSpan gap, int minimumLength = 1)
        {
            if (gap.TotalMilliseconds < 1 || gap.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(gap));
            if (minimumLength < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumLength));
            Gap = gap; MinimumLength = minimumLength;
        }
        /// <summary>마지막 수신 후 기다리는 시간입니다.</summary>
        public TimeSpan Gap { get; }
        /// <summary>경계로 인정할 최소 바이트 수입니다.</summary>
        public int MinimumLength { get; }
        /// <summary>시간 정보는 SerialClient가 관리하므로 바이트만으로 경계를 선언하지 않습니다.</summary>
        public int? GetFrameLength(ArraySegment<byte> buffer) => null;
    }
}
