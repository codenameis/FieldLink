using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Omron;
using System;
using System.IO;
using System.Linq;

namespace FieldLink.PlcDrivers.Omron.Clients
{
    /// <summary>FINS/TCP 노드 협상·명령·오류 알림의 프레임 경계를 계산합니다.</summary>
    public sealed class FinsTcpFrame : IFrameBoundary
    {
        private static readonly FinsTcpFrameRules Rules = new FinsTcpFrameRules();
        private readonly HeaderLengthFrame boundary = new HeaderLengthFrame(16, ReadTotalLength);

        /// <inheritdoc />
        public int? GetFrameLength(ArraySegment<byte> bufferedData) => boundary.GetFrameLength(bufferedData);

        private static int ReadTotalLength(ArraySegment<byte> header)
        {
            byte[] headerBytes = header.ToArray();
            if (!Rules.IsHeaderValid(headerBytes))
                throw new InvalidDataException("FINS/TCP 시그니처가 올바르지 않습니다.");
            return 16 + Rules.GetBodyLength(headerBytes);
        }
    }
}
