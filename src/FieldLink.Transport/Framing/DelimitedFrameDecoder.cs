using System;

namespace FieldLink.Transport.Framing;

/// <summary>특정 구분자(예: CR LF 또는 콜론과 CR LF)로 끝나는 프레임을 판정하는 디코더입니다.</summary>
public sealed class DelimitedFrameDecoder : IFrameDecoder
{
    private const int DefaultMaximumFrameLength = 4096;
    private const int DelimiterNotFoundIndex = -1;
    private const int InitialFrameLength = 0;
    private const int EmptyDelimiterLength = 0;

    private readonly byte[] _endDelimiter;
    private readonly byte[]? _startDelimiter;
    private readonly int _maximumFrameLength;

    /// <summary>종료 구분자와 최대 길이로 디코더를 초기화합니다.</summary>
    /// <param name="endDelimiter">프레임 끝을 알리는 바이트 배열입니다.</param>
    /// <param name="maximumFrameLength">허용할 프레임의 최대 바이트 수입니다.</param>
    /// <param name="startDelimiter">프레임 시작을 검증할 바이트 배열입니다. null이면 검사하지 않습니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="endDelimiter"/>가 null일 때.</exception>
    /// <exception cref="ArgumentException"><paramref name="endDelimiter"/>가 비어 있을 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumFrameLength"/>가 최소 프레임 길이 미만일 때.</exception>
    public DelimitedFrameDecoder(
        byte[] endDelimiter,
        int maximumFrameLength = DefaultMaximumFrameLength,
        byte[]? startDelimiter = null)
    {
        if (endDelimiter is null)
        {
            throw new ArgumentNullException(nameof(endDelimiter));
        }

        if (endDelimiter.Length == EmptyDelimiterLength)
        {
            throw new ArgumentException("종료 구분자는 비어 있을 수 없습니다.", nameof(endDelimiter));
        }

        long minimumFrameLength = (long)(startDelimiter?.Length ?? EmptyDelimiterLength) + endDelimiter.Length;
        if (maximumFrameLength < minimumFrameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFrameLength),
                maximumFrameLength,
                $"최대 프레임 길이는 시작·종료 구분자 길이 합({minimumFrameLength}) 이상이어야 합니다.");
        }

        _endDelimiter = (byte[])endDelimiter.Clone();
        _startDelimiter = startDelimiter is not null ? (byte[])startDelimiter.Clone() : null;
        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        ValidateStartDelimiter(received);

        int delimiterIndex = FindEndDelimiterIndex(received);
        if (delimiterIndex >= 0)
        {
            int detectedLength = delimiterIndex + _endDelimiter.Length;
            if (detectedLength > _maximumFrameLength)
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    $"감지된 프레임 길이({detectedLength} 바이트)가 최대 허용 길이({_maximumFrameLength} 바이트)를 초과했습니다.");
            }

            frameLength = detectedLength;
            return true;
        }

        if (received.Count >= _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"프레임이 최대 허용 길이({_maximumFrameLength} 바이트)를 초과할 때까지 종료 구분자를 찾지 못했습니다.");
        }

        frameLength = InitialFrameLength;
        return false;
    }

    private void ValidateStartDelimiter(ArraySegment<byte> received)
    {
        if (_startDelimiter is null || received.Count < _startDelimiter.Length)
        {
            return;
        }

        if (received.Array is not { } array)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        int offset = received.Offset;

        for (int i = 0; i < _startDelimiter.Length; i++)
        {
            if (array[offset + i] != _startDelimiter[i])
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    "수신된 데이터의 시작 구분자가 기대값과 일치하지 않습니다.");
            }
        }
    }

    private int FindEndDelimiterIndex(ArraySegment<byte> received)
    {
        if (received.Array is not { } array)
        {
            return DelimiterNotFoundIndex;
        }

        int offset = received.Offset;
        int count = received.Count;
        int delimiterLength = _endDelimiter.Length;
        int searchStart = _startDelimiter?.Length ?? EmptyDelimiterLength;

        for (int i = searchStart; i <= count - delimiterLength; i++)
        {
            if (IsDelimiterMatch(array, offset + i))
            {
                return i;
            }
        }

        return DelimiterNotFoundIndex;
    }

    private bool IsDelimiterMatch(byte[] array, int startIndex)
    {
        for (int j = 0; j < _endDelimiter.Length; j++)
        {
            if (array[startIndex + j] != _endDelimiter[j])
            {
                return false;
            }
        }

        return true;
    }
}
