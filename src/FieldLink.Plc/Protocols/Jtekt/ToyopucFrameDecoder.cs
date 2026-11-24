using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 프레임(4바이트 헤더) 경계를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class ToyopucFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 4;
    private const int LengthFieldOffset = 2;
    private const int LengthFieldByteCount = 2;
    private const int MaximumFrameLength = 65536;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: HeaderLength,
        lengthFieldOffset: LengthFieldOffset,
        lengthFieldByteCount: LengthFieldByteCount,
        isBigEndian: false,
        lengthAdjustment: HeaderLength,
        maximumFrameLength: MaximumFrameLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
