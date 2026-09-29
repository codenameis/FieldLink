using FieldLink.Communication.Framing;
using System;
using System.IO;

namespace FieldLink.Communication.Internal
{
    // 연결마다 하나씩 소유하며 첫 프레임 뒤의 잉여 바이트를 다음 수신에 보존한다.
    internal sealed class FrameAccumulator
    {
        private byte[] buffer;
        private readonly int maximum;
        internal int Count { get; private set; }
        internal bool IsFull => Count == maximum;
        internal FrameAccumulator(int maximum) { this.maximum = maximum; buffer = new byte[Math.Min(4096, maximum)]; }
        internal byte[] TryTake(IFrameBoundary boundary)
        {
            int? length = boundary.GetFrameLength(new ArraySegment<byte>(buffer, 0, Count));
            if (!length.HasValue)
                return null;
            if (length.Value < 1 || length.Value > maximum)
                throw new InvalidDataException("프레임 길이가 허용 범위를 벗어났습니다.");
            return Count >= length.Value ? Take(length.Value) : null;
        }
        internal byte[] Take(int length)
        {
            var frame = new byte[length]; Buffer.BlockCopy(buffer, 0, frame, 0, length);
            Count -= length; Buffer.BlockCopy(buffer, length, buffer, 0, Count); return frame;
        }
        internal ArraySegment<byte> FreeSpace()
        {
            if (Count == maximum)
                throw new InvalidDataException("최대 수신 길이 안에서 프레임이 완성되지 않았습니다.");
            if (Count == buffer.Length)
                Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, maximum));
            return new ArraySegment<byte>(buffer, Count, buffer.Length - Count);
        }
        internal void Advance(int length)
        {
            if (length < 1 || length > buffer.Length - Count)
                throw new IOException("포트가 잘못된 수신 길이를 반환했습니다.");
            Count += length;
        }
        internal void Clear() => Count = 0;
    }
}
