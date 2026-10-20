using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>TPKT 헤더의 길이 필드(빅엔디언, 헤더 포함 전체 길이)로 프레임 경계를 판정하는 디코더입니다. (사양서 §1)</summary>
public sealed class S7CommFrameDecoder : IFrameDecoder
{
    private const int TpktHeaderLength = 4;
    private const int LengthFieldOffset = 2;
    private const int LengthFieldByteCount = 2;
    private const int NoLengthAdjustment = 0;
    private const int MaxTpktLength = 65535;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        TpktHeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: true,
        NoLengthAdjustment,
        MaxTpktLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
