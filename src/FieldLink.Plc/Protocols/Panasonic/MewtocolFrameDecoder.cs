using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프레임(CR 종료) 경계를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class MewtocolFrameDecoder : IFrameDecoder
{
    private static readonly byte[] EndDelimiter = new byte[] { 0x0D }; // CR
    private readonly DelimitedFrameDecoder _innerDecoder = new(EndDelimiter, maximumFrameLength: 4096);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
