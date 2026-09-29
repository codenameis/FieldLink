using System;
using System.IO;

namespace FieldLink.Communication.Framing
{
    /// <summary>종료 문자 시퀀스와 선택적인 후행 바이트까지 하나의 프레임으로 판정합니다.</summary>
    public sealed class DelimitedFrame : IFrameBoundary
    {
        private readonly byte[] delimiter;
        private readonly int trailingByteCount;

        /// <summary>종료 문자는 반환 프레임에 포함됩니다. 체크섬 등이 뒤따르면 후행 바이트 수를 지정합니다.</summary>
        public DelimitedFrame(byte[] delimiter, int trailingByteCount = 0)
        {
            if (delimiter == null)
                throw new ArgumentNullException(nameof(delimiter));
            if (delimiter.Length == 0)
                throw new ArgumentException("A delimiter must not be empty.", nameof(delimiter));
            if (trailingByteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(trailingByteCount));
            this.delimiter = (byte[])delimiter.Clone();
            this.trailingByteCount = trailingByteCount;
        }

        /// <inheritdoc />
        public int? GetFrameLength(ArraySegment<byte> bufferedData)
        {
            for (int start = 0; start <= bufferedData.Count - delimiter.Length; start++)
            {
                int matched = 0;
                while (matched < delimiter.Length &&
                    bufferedData.Array[bufferedData.Offset + start + matched] == delimiter[matched]) matched++;
                if (matched == delimiter.Length)
                {
                    long length = (long)start + delimiter.Length + trailingByteCount;
                    if (length > int.MaxValue)
                        throw new InvalidDataException("Delimited frame length overflows Int32.");
                    return (int)length;
                }
            }
            return null;
        }
    }
}
