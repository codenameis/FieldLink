using System;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKAVARPROXY 4바이트 Big-Endian 헤더의 본문 길이 필드로 프레임 경계를 판정하는 디코더입니다.</summary>
public sealed class KukaVarProxyFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 4;
    private const int BodyLengthOffset = 2;
    private const int DefaultMaximumFrameLength = 65536;

    private readonly int _maximumFrameLength;

    /// <summary>최대 허용 프레임 길이로 디코더를 초기화합니다.</summary>
    public KukaVarProxyFrameDecoder(int maximumFrameLength = DefaultMaximumFrameLength)
    {
        if (maximumFrameLength < HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength), maximumFrameLength, "최대 프레임 길이는 4 이상이어야 합니다.");
        }

        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < HeaderLength)
        {
            frameLength = 0;
            return false;
        }

        ushort bodyLength = RobotBinaryBuffer.ReadUInt16BigEndian(received.Array!, received.Offset + BodyLengthOffset);
        int totalLength = HeaderLength + bodyLength;
        if (totalLength > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"KUKAVARPROXY 프레임 길이({totalLength} 바이트)가 최대 허용 범위({_maximumFrameLength})를 초과했습니다.");
        }

        if (received.Count < totalLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = totalLength;
        return true;
    }
}
