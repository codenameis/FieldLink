using System;

namespace FieldLink.Robot.Conversion;

/// <summary>바이트 배열과 불리언 비트 배열 간의 LSB 우선 패킹 및 언패킹 순수 함수입니다.</summary>
internal static class RobotBitPacking
{
    public static int GetPackedByteCount(int bitCount)
    {
        if (bitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "비트 수는 0 이상이어야 합니다.");
        }

        return (bitCount + 7) / 8;
    }

    public static byte[] Pack(bool[] bits)
    {
        if (bits is null)
        {
            throw new ArgumentNullException(nameof(bits));
        }

        byte[] packed = new byte[GetPackedByteCount(bits.Length)];
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i])
            {
                packed[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return packed;
    }

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
        if (offset < 0 || offset + requiredBytes > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "비트 언패킹 범위가 버퍼를 초과했습니다.");
        }

        bool[] bits = new bool[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            bits[i] = (buffer[offset + (i / 8)] & (1 << (i % 8))) != 0;
        }

        return bits;
    }
}
