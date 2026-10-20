using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc SRTP 프레임(56바이트 헤더 + 확장 페이로드) 경계를 검출하는 디코더입니다. (사양서 §2.1)</summary>
public sealed class GESrtpFrameDecoder : IFrameDecoder
{
    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: 56,
        lengthFieldOffset: 4,
        lengthFieldByteCount: 2,
        isBigEndian: false,
        lengthAdjustment: 56,
        maximumFrameLength: 65536);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
