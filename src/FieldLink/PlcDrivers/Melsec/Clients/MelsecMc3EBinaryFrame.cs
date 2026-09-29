using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Melsec;
using System;
using System.IO;
using System.Linq;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>QnA 호환 3E 바이너리 응답의 길이 파서를 TCP 프레임 경계에 연결합니다.</summary>
    public sealed class MelsecMc3EBinaryFrame : IFrameBoundary
    {
        internal const int HeaderLength = 9;
        internal const int ResponseDataOffset = 11;
        private static readonly MelsecQnA3EBinaryFrameRules Rules = new MelsecQnA3EBinaryFrameRules();
        private readonly HeaderLengthFrame boundary = new HeaderLengthFrame(HeaderLength, ReadTotalLength);

        /// <inheritdoc />
        public int? GetFrameLength(ArraySegment<byte> bufferedData) => boundary.GetFrameLength(bufferedData);

        private static int ReadTotalLength(ArraySegment<byte> header)
        {
            byte[] headerBytes = header.ToArray();
            if (!Rules.IsHeaderValid(headerBytes))
                throw new InvalidDataException("QnA3E 바이너리 응답의 서브헤더는 D0 00이어야 합니다.");
            int bodyLength = Rules.GetBodyLength(headerBytes);
            if (bodyLength < ResponseDataOffset - HeaderLength)
                throw new InvalidDataException("QnA3E 응답에는 2바이트 종료 코드가 필요합니다.");
            return HeaderLength + bodyLength;
        }
    }
}
