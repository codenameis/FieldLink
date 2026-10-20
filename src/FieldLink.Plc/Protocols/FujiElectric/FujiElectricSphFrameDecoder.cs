using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 프레임(20바이트 SX-Bus 헤더) 경계를 검출하는 디코더입니다. (사양서 Part C §C.1.1)</summary>
public sealed class FujiElectricSphFrameDecoder : IFrameDecoder
{
    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        headerLength: 20,
        lengthFieldOffset: 18,
        lengthFieldByteCount: 2,
        isBigEndian: false,
        lengthAdjustment: 20,
        maximumFrameLength: 2048);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
