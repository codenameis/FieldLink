using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 공통 헤더의 길이 필드를 해석하여 프레임 경계를 판정하는 디코더입니다. (사양서 §1, §3-1)</summary>
public sealed class FEnetFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 20;
    private const int LengthFieldOffset = 16;
    private const int LengthFieldByteCount = 2;
    private const int LengthAdjustment = 20; // 전체 프레임 = 헤더 20바이트 + Length 필드 값
    private const int MaximumFrameSize = 4096;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        HeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: false, // XGT FEnet 길이 필드는 리틀 엔디언
        LengthAdjustment,
        MaximumFrameSize);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
