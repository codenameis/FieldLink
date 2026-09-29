using System;
using System.IO;

namespace FieldLink.Communication.Framing
{
    /// <summary>고정 길이 헤더에서 메시지 전체 길이를 계산합니다.</summary>
    public sealed class HeaderLengthFrame : IFrameBoundary
    {
        private readonly int headerLength;
        private readonly Func<ArraySegment<byte>, int> readTotalLength;

        /// <summary>헤더 길이와 전체 프레임 길이 계산 함수를 지정합니다. 함수에서 헤더 유효성도 검사할 수 있습니다.</summary>
        public HeaderLengthFrame(int headerLength, Func<ArraySegment<byte>, int> readTotalLength)
        {
            if (headerLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(headerLength));
            this.headerLength = headerLength;
            this.readTotalLength = readTotalLength ?? throw new ArgumentNullException(nameof(readTotalLength));
        }

        /// <inheritdoc />
        public int? GetFrameLength(ArraySegment<byte> bufferedData)
        {
            if (bufferedData.Count < headerLength)
                return null;
            int totalLength = readTotalLength(new ArraySegment<byte>(bufferedData.Array, bufferedData.Offset, headerLength));
            if (totalLength < headerLength)
                throw new InvalidDataException("Frame length is smaller than its header.");
            return totalLength;
        }
    }
}
