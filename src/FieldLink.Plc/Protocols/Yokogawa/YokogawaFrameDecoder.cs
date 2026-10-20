using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 프레임 경계(4바이트 헤더)를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class YokogawaFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 4;
    private const int LengthFieldOffset = 2;
    private const int LengthFieldByteCount = 2;
    private const int LengthAdjustment = 4;
    private const int MaximumFrameLength = 65535;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        HeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: true,
        LengthAdjustment,
        MaximumFrameLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
