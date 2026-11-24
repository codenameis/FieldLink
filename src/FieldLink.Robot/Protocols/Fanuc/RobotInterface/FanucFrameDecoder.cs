using System;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC SRTP 56바이트 고정 헤더와 가변 페이로드의 프레임 경계를 판정하는 디코더입니다.</summary>
public sealed class FanucFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 56;
    private const int PayloadLengthOffset = 4;
    private const int DefaultMaximumFrameLength = 65536;

    private readonly int _maximumFrameLength;

    /// <summary>최대 허용 프레임 길이로 디코더를 초기화합니다.</summary>
    public FanucFrameDecoder(int maximumFrameLength = DefaultMaximumFrameLength)
    {
        if (maximumFrameLength < HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength), maximumFrameLength, "최대 프레임 길이는 56 이상이어야 합니다.");
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

        ushort payloadLength = RobotBinaryBuffer.ReadUInt16LittleEndian(received.Array!, received.Offset + PayloadLengthOffset);
        int totalLength = HeaderLength + payloadLength;
        if (totalLength > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"FANUC 프레임 길이({totalLength} 바이트)가 최대 허용 범위({_maximumFrameLength})를 초과했습니다.");
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
