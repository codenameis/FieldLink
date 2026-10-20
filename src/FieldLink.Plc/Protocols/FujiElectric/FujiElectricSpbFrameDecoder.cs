using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB ASCII 프레임(':' 시작, CR LF 종료) 경계를 검출하는 디코더입니다. (사양서 §A.1.1, §A.2.1)</summary>
public sealed class FujiElectricSpbFrameDecoder : IFrameDecoder
{
    private const int MaximumFrameLength = 1024;
    private static readonly byte[] StartDelimiter = new byte[] { 0x3A }; // ':'
    private static readonly byte[] EndDelimiter = new byte[] { 0x0D, 0x0A }; // CR LF

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
