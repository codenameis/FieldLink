using System;

namespace FieldLink.Transport.Framing;

/// <summary>고정 헤더 내의 길이 필드를 읽어 프레임 전체 길이를 계산하는 디코더입니다.</summary>
/// <remarks>
/// Modbus TCP MBAP 헤더, Mitsubishi MC 3E 서브헤더 등 헤더에 페이로드 길이가 적힌 프로토콜에 사용합니다.
/// </remarks>
public sealed class HeaderLengthFrameDecoder : IFrameDecoder
{
    private readonly int _headerLength;
    private readonly int _lengthFieldOffset;
    private readonly int _lengthFieldByteCount;
    private readonly bool _isBigEndian;
    private readonly int _lengthAdjustment;
    private readonly int _maximumFrameLength;

    /// <summary>헤더 위치와 길이 필드 사양으로 디코더를 초기화합니다.</summary>
    /// <param name="headerLength">길이 필드를 포함한 최소 헤더 바이트 수입니다.</param>
    /// <param name="lengthFieldOffset">길이 필드의 헤더 내 시작 오프셋입니다.</param>
    /// <param name="lengthFieldByteCount">길이 필드의 바이트 수입니다. 1, 2, 4 중 하나입니다.</param>
    /// <param name="isBigEndian">길이 필드가 빅엔디언이면 true, 리틀엔디언이면 false입니다.</param>
    /// <param name="lengthAdjustment">추출한 길이 값에 더해 전체 프레임 길이를 산출할 보정값입니다.</param>
    /// <param name="maximumFrameLength">허용할 프레임의 최대 바이트 수입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">인수 범위가 올바르지 않을 때.</exception>
    public HeaderLengthFrameDecoder(
        int headerLength,
        int lengthFieldOffset,
        int lengthFieldByteCount,
        bool isBigEndian,
        int lengthAdjustment,
        int maximumFrameLength = 65536)
    {
        if (headerLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(headerLength), headerLength, "헤더 길이는 1 이상이어야 합니다.");
        }

        if (lengthFieldOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthFieldOffset), lengthFieldOffset, "길이 필드 오프셋은 0 이상이어야 합니다.");
        }

        if (lengthFieldByteCount is not (1 or 2 or 4))
        {
            throw new ArgumentOutOfRangeException(nameof(lengthFieldByteCount), lengthFieldByteCount, "길이 필드 바이트 수는 1, 2, 4 중 하나여야 합니다.");
        }

        if (lengthFieldOffset + lengthFieldByteCount > headerLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthFieldOffset),
                lengthFieldOffset,
                "길이 필드는 헤더 영역 안에 완전히 포함되어야 합니다.");
        }

        if (maximumFrameLength < headerLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFrameLength),
                maximumFrameLength,
                "최대 프레임 길이는 헤더 길이 이상이어야 합니다.");
        }

        _headerLength = headerLength;
        _lengthFieldOffset = lengthFieldOffset;
        _lengthFieldByteCount = lengthFieldByteCount;
        _isBigEndian = isBigEndian;
        _lengthAdjustment = lengthAdjustment;
        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < _headerLength)
        {
            frameLength = 0;
            return false;
        }

        int rawLength = ReadLengthValue(received);
        int expectedTotalLength = rawLength + _lengthAdjustment;

        if (expectedTotalLength < _headerLength || expectedTotalLength > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"계산된 프레임 길이({expectedTotalLength} 바이트)가 유효 범위({_headerLength}~{_maximumFrameLength})를 벗어났습니다.");
        }

        if (received.Count < expectedTotalLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = expectedTotalLength;
        return true;
    }

    private int ReadLengthValue(ArraySegment<byte> received)
    {
        byte[] array = received.Array!;
        int offset = received.Offset + _lengthFieldOffset;

        return _lengthFieldByteCount switch
        {
            1 => array[offset],
            2 => _isBigEndian
                ? (array[offset] << 8) | array[offset + 1]
                : array[offset] | (array[offset + 1] << 8),
            4 => _isBigEndian
                ? (array[offset] << 24) | (array[offset + 1] << 16) | (array[offset + 2] << 8) | array[offset + 3]
                : array[offset] | (array[offset + 1] << 8) | (array[offset + 2] << 16) | (array[offset + 3] << 24),
            _ => throw new InvalidOperationException("지원하지 않는 길이 필드 크기입니다.")
        };
    }
}
