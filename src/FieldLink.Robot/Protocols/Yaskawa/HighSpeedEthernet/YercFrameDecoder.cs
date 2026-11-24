using System;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA <c>YERC</c> 32바이트 헤더 및 가변 데이터부 프레임 경계를 판정하는 디코더입니다.</summary>
public sealed class YercFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 32;
    private const int HeaderSizeOffset = 4;
    private const int PayloadLengthOffset = 6;
    private const int DefaultMaximumFrameLength = 65536;
    private const byte MagicByte0 = (byte)'Y';
    private const byte MagicByte1 = (byte)'E';
    private const byte MagicByte2 = (byte)'R';
    private const byte MagicByte3 = (byte)'C';

    private readonly int _maximumFrameLength;

    /// <summary>최대 허용 프레임 길이로 디코더를 초기화합니다.</summary>
    public YercFrameDecoder(int maximumFrameLength = DefaultMaximumFrameLength)
    {
        if (maximumFrameLength < HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength), maximumFrameLength, "최대 프레임 길이는 32 이상이어야 합니다.");
        }

        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < HeaderLength || received.Array is not byte[] array)
        {
            frameLength = 0;
            return false;
        }

        int offset = received.Offset;
        if (array[offset] != MagicByte0 || array[offset + 1] != MagicByte1 || array[offset + 2] != MagicByte2 || array[offset + 3] != MagicByte3)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "YERC 매직 헤더가 일치하지 않습니다.");
        }

        ushort headerSize = RobotBinaryBuffer.ReadUInt16LittleEndian(array, offset + HeaderSizeOffset);
        if (headerSize != HeaderLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"YERC 헤더 크기({headerSize} 바이트)가 규격({HeaderLength} 바이트)과 일치하지 않습니다.");
        }

        ushort payloadLength = RobotBinaryBuffer.ReadUInt16LittleEndian(array, offset + PayloadLengthOffset);
        int totalLength = HeaderLength + payloadLength;
        if (totalLength > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"YERC 프레임 길이({totalLength} 바이트)가 최대 허용 범위({_maximumFrameLength})를 초과했습니다.");
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
