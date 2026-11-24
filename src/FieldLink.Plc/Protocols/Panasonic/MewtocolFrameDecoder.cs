using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프레임(CR 종료) 경계를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class MewtocolFrameDecoder : IFrameDecoder
{
    private const byte CarriageReturn = 0x0D;
    private const int DefaultMaximumFrameLength = 4096;

    private static readonly byte[] EndDelimiter = [CarriageReturn];
    private readonly DelimitedFrameDecoder _innerDecoder = new(EndDelimiter, maximumFrameLength: DefaultMaximumFrameLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
