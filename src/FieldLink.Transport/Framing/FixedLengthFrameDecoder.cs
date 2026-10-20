using System;

namespace FieldLink.Transport.Framing;

/// <summary>항상 고정된 바이트 수의 프레임을 판정하는 디코더입니다.</summary>
public sealed class FixedLengthFrameDecoder : IFrameDecoder
{
    private readonly int _frameLength;

    /// <summary>고정 프레임 길이로 디코더를 초기화합니다.</summary>
    /// <param name="frameLength">프레임 하나의 바이트 수입니다. 1 이상이어야 합니다.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameLength"/>가 1 미만일 때.</exception>
    public FixedLengthFrameDecoder(int frameLength)
    {
        if (frameLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameLength), frameLength, "프레임 길이는 1 이상이어야 합니다.");
        }

        _frameLength = frameLength;
    }

    /// <summary>프레임 하나의 기대 바이트 수입니다.</summary>
    public int FrameLength => _frameLength;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < _frameLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = _frameLength;
        return true;
    }
}
