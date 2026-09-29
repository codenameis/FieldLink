using System;

namespace FieldLink.Communication.Framing
{
    /// <summary>이스케이프 바이트가 중복되는 이진 프레임에서 종료 쌍과 후행 체크섬까지 판정합니다.</summary>
    public sealed class EscapedTerminatorFrame : IFrameBoundary
    {
        private readonly byte escape;
        private readonly byte terminator;
        private readonly int trailingBytes;
        /// <summary>중복 이스케이프는 데이터로, 이스케이프·종료 코드 쌍은 종료로 해석합니다.</summary>
        public EscapedTerminatorFrame(byte escape, byte terminator, int trailingBytes)
        {
            if (escape == terminator)
                throw new ArgumentException("이스케이프와 종료 코드는 달라야 합니다.");
            if (trailingBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(trailingBytes));
            this.escape = escape; this.terminator = terminator; this.trailingBytes = trailingBytes;
        }
        /// <inheritdoc/>
        public int? GetFrameLength(ArraySegment<byte> bytes)
        {
            for (int i = 0; i + 1 < bytes.Count; i++)
            {
                if (bytes.Array[bytes.Offset + i] != escape)
                    continue;
                byte next = bytes.Array[bytes.Offset + i + 1];
                if (next == escape)
                    i++;
                else if (next == terminator)
                    return checked(i + 2 + trailingBytes);
            }
            return null;
        }
    }
}
