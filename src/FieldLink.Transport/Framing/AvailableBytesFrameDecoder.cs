using System;

namespace FieldLink.Transport.Framing;

/// <summary>수신 버퍼에 1바이트 이상 도착하면 전체 바이트를 하나의 프레임으로 판정하는 디코더입니다.</summary>
/// <remarks>명시적인 길이 헤더나 종료 구분자가 없는 원시 TCP ASCII 스트림 프로토콜(예: KUKA Socket ASCII)에 사용합니다.</remarks>
public sealed class AvailableBytesFrameDecoder : IFrameDecoder
{
    private const int DefaultMaximumFrameLength = 65536;

    private readonly int _maximumFrameLength;

    /// <summary>허용할 최대 프레임 길이로 디코더를 초기화합니다.</summary>
    /// <param name="maximumFrameLength">한 번에 수락할 최대 바이트 수입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumFrameLength"/>가 1 미만일 때.</exception>
    public AvailableBytesFrameDecoder(int maximumFrameLength = DefaultMaximumFrameLength)
    {
        if (maximumFrameLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength), maximumFrameLength, "최대 프레임 길이는 1 이상이어야 합니다.");
        }

        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count <= 0)
        {
            frameLength = 0;
            return false;
        }

        if (received.Count > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"수신된 프레임 크기({received.Count} 바이트)가 최대 허용 길이({_maximumFrameLength} 바이트)를 초과했습니다.");
        }

        frameLength = received.Count;
        return true;
    }
}
