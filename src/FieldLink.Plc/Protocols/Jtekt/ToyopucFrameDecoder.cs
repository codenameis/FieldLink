using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 프레임(4바이트 헤더) 경계를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class ToyopucFrameDecoder : IFrameDecoder
{
    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: 4,
        lengthFieldOffset: 2,
        lengthFieldByteCount: 2,
        isBigEndian: false,
        lengthAdjustment: 4,
        maximumFrameLength: 4096);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
