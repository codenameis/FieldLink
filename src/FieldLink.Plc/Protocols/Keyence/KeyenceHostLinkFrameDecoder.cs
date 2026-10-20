using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크 ASCII 응답(CRLF 종료) 경계를 검출하는 디코더입니다. (사양서 Part A §A.2.1)</summary>
public sealed class KeyenceHostLinkFrameDecoder : IFrameDecoder
{
    private static readonly byte[] EndDelimiter = new byte[] { 0x0D, 0x0A }; // CR LF
    private readonly DelimitedFrameDecoder _innerDecoder = new(EndDelimiter, maximumFrameLength: 4096);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
