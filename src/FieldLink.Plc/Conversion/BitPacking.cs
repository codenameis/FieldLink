using System;

namespace FieldLink.Plc.Conversion;

/// <summary>불리언(비트) 배열과 패킹된 바이트 배열 간의 상호 변환을 지원하는 순수 함수 모음입니다.</summary>
/// <remarks>
/// 바이트의 최하위 비트(LSB)부터 순서대로 비트 인덱스 0이 배치되는 표준 산업용 패킹 방식을 사용합니다.
/// </remarks>
public static class BitPacking
{
    /// <summary>비트 수에 필요한 패킹 바이트 수를 구합니다.</summary>
    /// <param name="bitCount">비트 수입니다.</param>
    /// <returns>올림 계산된 바이트 수입니다.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/>가 음수일 때.</exception>
    public static int GetPackedByteCount(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "비트 수는 0 이상이어야 합니다.");
        }

        return (bitCount + 7) / 8;
    }

    /// <summary>불리언 배열을 LSB 우선 바이트 배열로 패킹합니다.</summary>
    /// <param name="bits">패킹할 비트 값 배열입니다.</param>
    /// <returns>패킹된 바이트 배열입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bits"/>가 null일 때.</exception>
    public static byte[] Pack(bool[] bits)
    {
        if (bits is null)
        {
            throw new ArgumentNullException(nameof(bits));
        }

        int byteCount = GetPackedByteCount(bits.Length);
        byte[] bytes = new byte[byteCount];

        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                int byteIndex = i / 8;
                int bitOffset = i % 8;
                bytes[byteIndex] |= (byte)(1 << bitOffset);
            }
        }

        return bytes;
    }

    /// <summary>패킹된 바이트 배열에서 비트들을 불리언 배열로 언패킹합니다.</summary>
    /// <param name="buffer">바이트 배열입니다.</param>
    /// <param name="offset">시작 바이트 오프셋입니다.</param>
    /// <param name="bitCount">추출할 총 비트 수입니다.</param>
    /// <returns>언패킹된 불리언 배열입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">오프셋 또는 비트 수 범위가 유효하지 않을 때.</exception>
    public static bool[] Unpack(byte[] buffer, int offset, int bitCount)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "비트 수는 0 이상이어야 합니다.");
        }

        int requiredBytes = GetPackedByteCount(bitCount);
        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 배열 크기 이하여야 합니다.");
        }

        if (offset + requiredBytes > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitCount),
                bitCount,
                $"비트 수({bitCount})에 필요한 바이트({requiredBytes})가 배열 범위를 초과합니다.");
        }

        bool[] result = new bool[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            int byteIndex = offset + (i / 8);
            int bitOffset = i % 8;
            result[i] = (buffer[byteIndex] & (1 << bitOffset)) != 0;
        }

        return result;
    }

    /// <summary>패킹된 바이트 배열의 처음부터 비트들을 불리언 배열로 언패킹합니다.</summary>
    /// <param name="buffer">바이트 배열입니다.</param>
    /// <param name="bitCount">추출할 총 비트 수입니다.</param>
    /// <returns>언패킹된 불리언 배열입니다.</returns>
    public static bool[] Unpack(byte[] buffer, int bitCount)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return Unpack(buffer, 0, bitCount);
    }
}
