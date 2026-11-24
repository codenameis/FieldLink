using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 프레임(56바이트 헤더 + 확장 페이로드) 경계를 검출하는 디코더입니다. (사양서 §2.1)</summary>
public sealed class GESrtpFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 56;
    private const int ExtendedLengthFieldOffset = 4;
    private const int ExtendedLengthFieldByteCount = 2;
    private const int MaximumFrameLength = 65536;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: HeaderLength,
        lengthFieldOffset: ExtendedLengthFieldOffset,
        lengthFieldByteCount: ExtendedLengthFieldByteCount,
        isBigEndian: false,
        lengthAdjustment: HeaderLength,
        maximumFrameLength: MaximumFrameLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
