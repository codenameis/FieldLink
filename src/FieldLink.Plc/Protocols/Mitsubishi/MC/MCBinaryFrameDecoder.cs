using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 바이너리 헤더의 길이 필드(리틀 엔디언)로 프레임 경계를 판정하는 디코더입니다. (사양서 §1)</summary>
public sealed class MCBinaryFrameDecoder : IFrameDecoder
{
    private const int Header3ELength = 9;
    private const int Header4ELength = 13;
    private const int LengthFieldByteCount = 2;
    private const int MaxLengthFieldValue = 0xFFFF;

    private readonly HeaderLengthFrameDecoder _innerDecoder;

    /// <summary>프레임 종류에 맞는 디코더를 만듭니다.</summary>
    /// <param name="frameType">3E 또는 4E 프레임 종류입니다.</param>
    public MCBinaryFrameDecoder(MCFrameType frameType)
    {
        int headerLength = frameType == MCFrameType.Frame4E ? Header4ELength : Header3ELength;

        // 길이 필드는 헤더의 맨 끝 2바이트이고, 전체 길이 = 헤더 + 길이 필드 값이다.
        _innerDecoder = new HeaderLengthFrameDecoder(
            headerLength,
            headerLength - LengthFieldByteCount,
            LengthFieldByteCount,
            isBigEndian: false,
            lengthAdjustment: headerLength,
            maximumFrameLength: headerLength + MaxLengthFieldValue);
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
