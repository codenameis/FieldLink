using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK Program Port ASCII 프레임(STX~ETX) 경계를 검출하는 디코더입니다. (사양서 §1.1, §2.1)</summary>
public sealed class FatekFrameDecoder : IFrameDecoder
{
    private const int MaximumFrameLength = 1024;
    private static readonly byte[] StartDelimiter = new byte[] { 0x02 }; // STX
    private static readonly byte[] EndDelimiter = new byte[] { 0x03 }; // ETX

    private readonly DelimitedFrameDecoder _innerDecoder = new(
        EndDelimiter,
        MaximumFrameLength,
        StartDelimiter);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
