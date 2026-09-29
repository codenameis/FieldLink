using System;

namespace FieldLink.Communication.Framing
{
    /// <summary>항상 같은 길이인 TCP 메시지의 경계를 판정합니다.</summary>
    public sealed class FixedLengthFrame : IFrameBoundary
    {
        private readonly int length;

        /// <summary>0보다 큰 메시지 전체 길이를 지정합니다.</summary>
        public FixedLengthFrame(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            this.length = length;
        }

        /// <inheritdoc />
        public int? GetFrameLength(ArraySegment<byte> bufferedData) => length;
    }
}
