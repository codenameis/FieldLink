using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 프레임(20바이트 SX-Bus 헤더) 경계를 검출하는 디코더입니다. (사양서 Part C §C.1.1)</summary>
public sealed class FujiElectricSphFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 20;
    private const int PayloadLengthOffset = 18;
    private const int PayloadLengthFieldByteCount = 2;
    private const int MaximumFrameLength = 2048;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: HeaderLength,
        lengthFieldOffset: PayloadLengthOffset,
        lengthFieldByteCount: PayloadLengthFieldByteCount,
        isBigEndian: false,
        lengthAdjustment: HeaderLength,
        maximumFrameLength: MaximumFrameLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
