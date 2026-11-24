using System;
using System.Runtime.InteropServices;

namespace FieldLink.Plc.Conversion;

/// <summary>바이트 순서(ByteOrder)를 고려하여 PLC 바이트와 .NET 기본 형식 간 상호 변환을 수행하는 변환기입니다.</summary>
public static partial class PlcValueConverter
{
    private const int BooleanByteSize = 1;
    private const int Int16ByteSize = 2;
    private const int Int32ByteSize = 4;
    private const int Int64ByteSize = 8;
    private const int MaxBitIndex = 7;

    /// <summary>버퍼에서 16비트 부호 있는 정수를 읽습니다.</summary>
    public static short ToInt16(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        return (short)ToUInt16(buffer, offset, byteOrder);
    }

    /// <summary>버퍼에서 16비트 부호 없는 정수를 읽습니다.</summary>
    public static ushort ToUInt16(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateBuffer(buffer, offset, 2);
        byte firstByte = buffer[offset];
        byte secondByte = buffer[offset + 1];

        return IsLittleEndianWithinWord(byteOrder)
            ? (ushort)(firstByte | (secondByte << 8))
            : (ushort)((firstByte << 8) | secondByte);
    }

    /// <summary>버퍼에서 32비트 부호 있는 정수를 읽습니다.</summary>
    public static int ToInt32(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        return (int)ToUInt32(buffer, offset, byteOrder);
    }

    /// <summary>버퍼에서 32비트 부호 없는 정수를 읽습니다.</summary>
    public static uint ToUInt32(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateBuffer(buffer, offset, 4);
        EnsureDefinedByteOrder(byteOrder);

        ushort firstWord = ToUInt16(buffer, offset, byteOrder);
        ushort secondWord = ToUInt16(buffer, offset + 2, byteOrder);

        return IsLowWordFirst(byteOrder)
            ? ((uint)secondWord << 16) | firstWord
            : ((uint)firstWord << 16) | secondWord;
    }

    /// <summary>버퍼에서 64비트 부호 있는 정수를 읽습니다.</summary>
    public static long ToInt64(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        return (long)ToUInt64(buffer, offset, byteOrder);
    }

    /// <summary>버퍼에서 64비트 부호 없는 정수를 읽습니다.</summary>
    public static ulong ToUInt64(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateBuffer(buffer, offset, 8);
        EnsureDefinedByteOrder(byteOrder);

        ushort word0 = ToUInt16(buffer, offset, byteOrder);
        ushort word1 = ToUInt16(buffer, offset + 2, byteOrder);
        ushort word2 = ToUInt16(buffer, offset + 4, byteOrder);
        ushort word3 = ToUInt16(buffer, offset + 6, byteOrder);

        return IsLowWordFirst(byteOrder)
            ? ((ulong)word3 << 48) | ((ulong)word2 << 32) | ((ulong)word1 << 16) | word0
            : ((ulong)word0 << 48) | ((ulong)word1 << 32) | ((ulong)word2 << 16) | word3;
    }

    /// <summary>버퍼에서 단정밀도 부동소수점(float)을 읽습니다.</summary>
    public static float ToSingle(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        int raw = ToInt32(buffer, offset, byteOrder);
        var union = new FloatUnion { IntValue = raw };
        return union.FloatValue;
    }

    /// <summary>버퍼에서 배정밀도 부동소수점(double)을 읽습니다.</summary>
    public static double ToDouble(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        long raw = ToInt64(buffer, offset, byteOrder);
        var union = new DoubleUnion { LongValue = raw };
        return union.DoubleValue;
    }

    /// <summary>버퍼의 특정 오프셋 및 비트 인덱스에서 불리언 값을 읽습니다.</summary>
    public static bool ToBoolean(byte[] buffer, int offset = 0, int bitIndex = 0)
    {
        ValidateBuffer(buffer, offset, BooleanByteSize);
        if (bitIndex is < 0 or > MaxBitIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), bitIndex, $"비트 인덱스는 0에서 {MaxBitIndex} 사이여야 합니다.");
        }

        return (buffer[offset] & (1 << bitIndex)) != 0;
    }

    /// <summary>16비트 부호 있는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(short value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[2];
        WriteInt16(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>16비트 부호 없는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(ushort value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[2];
        WriteUInt16(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>32비트 부호 있는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(int value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[4];
        WriteInt32(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>32비트 부호 없는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(uint value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[4];
        WriteUInt32(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>64비트 부호 있는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(long value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[8];
        WriteInt64(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>64비트 부호 없는 정수를 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(ulong value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        byte[] bytes = new byte[8];
        WriteUInt64(bytes, 0, value, byteOrder);
        return bytes;
    }

    /// <summary>단정밀도 부동소수점(float)을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(float value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        var union = new FloatUnion { FloatValue = value };
        return GetBytes(union.IntValue, byteOrder);
    }

    /// <summary>배정밀도 부동소수점(double)을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(double value, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        var union = new DoubleUnion { DoubleValue = value };
        return GetBytes(union.LongValue, byteOrder);
    }

    /// <summary>불리언 값을 1바이트 배열(0x01 또는 0x00)로 변환합니다.</summary>
    public static byte[] GetBytes(bool value)
    {
        return new byte[] { (byte)(value ? 1 : 0) };
    }

    internal static void WriteInt16(byte[] destination, int offset, short value, ByteOrder byteOrder)
    {
        WriteUInt16(destination, offset, (ushort)value, byteOrder);
    }

    internal static void WriteUInt16(byte[] destination, int offset, ushort value, ByteOrder byteOrder)
    {
        EnsureDefinedByteOrder(byteOrder);
        byte highByte = (byte)((value >> 8) & 0xFF);
        byte lowByte = (byte)(value & 0xFF);

        if (IsLittleEndianWithinWord(byteOrder))
        {
            destination[offset] = lowByte;
            destination[offset + 1] = highByte;
        }
        else
        {
            destination[offset] = highByte;
            destination[offset + 1] = lowByte;
        }
    }

    internal static void WriteInt32(byte[] destination, int offset, int value, ByteOrder byteOrder)
    {
        WriteUInt32(destination, offset, (uint)value, byteOrder);
    }

    internal static void WriteUInt32(byte[] destination, int offset, uint value, ByteOrder byteOrder)
    {
        EnsureDefinedByteOrder(byteOrder);
        ushort highWord = (ushort)(value >> 16);
        ushort lowWord = (ushort)(value & 0xFFFF);

        if (IsLowWordFirst(byteOrder))
        {
            WriteUInt16(destination, offset, lowWord, byteOrder);
            WriteUInt16(destination, offset + 2, highWord, byteOrder);
        }
        else
        {
            WriteUInt16(destination, offset, highWord, byteOrder);
            WriteUInt16(destination, offset + 2, lowWord, byteOrder);
        }
    }

    internal static void WriteInt64(byte[] destination, int offset, long value, ByteOrder byteOrder)
    {
        WriteUInt64(destination, offset, (ulong)value, byteOrder);
    }

    internal static void WriteUInt64(byte[] destination, int offset, ulong value, ByteOrder byteOrder)
    {
        EnsureDefinedByteOrder(byteOrder);
        ushort highestWord = (ushort)(value >> 48);
        ushort upperMiddleWord = (ushort)(value >> 32);
        ushort lowerMiddleWord = (ushort)(value >> 16);
        ushort lowestWord = (ushort)value;

        if (IsLowWordFirst(byteOrder))
        {
            WriteUInt16(destination, offset, lowestWord, byteOrder);
            WriteUInt16(destination, offset + 2, lowerMiddleWord, byteOrder);
            WriteUInt16(destination, offset + 4, upperMiddleWord, byteOrder);
            WriteUInt16(destination, offset + 6, highestWord, byteOrder);
        }
        else
        {
            WriteUInt16(destination, offset, highestWord, byteOrder);
            WriteUInt16(destination, offset + 2, upperMiddleWord, byteOrder);
            WriteUInt16(destination, offset + 4, lowerMiddleWord, byteOrder);
            WriteUInt16(destination, offset + 6, lowestWord, byteOrder);
        }
    }

    private static bool IsLittleEndianWithinWord(ByteOrder byteOrder) =>
        byteOrder is ByteOrder.LittleEndian or ByteOrder.LittleEndianWordSwap;

    private static bool IsLowWordFirst(ByteOrder byteOrder) =>
        byteOrder is ByteOrder.BigEndianWordSwap or ByteOrder.LittleEndian;

    private static void EnsureDefinedByteOrder(ByteOrder byteOrder)
    {
        if (byteOrder is not (ByteOrder.BigEndian
            or ByteOrder.BigEndianWordSwap
            or ByteOrder.LittleEndian
            or ByteOrder.LittleEndianWordSwap))
        {
            throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "지원하지 않는 바이트 순서입니다.");
        }
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int requiredBytes)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 버퍼 길이 이하여야 합니다.");
        }

        if (requiredBytes < 0 || requiredBytes > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                $"필요 바이트 수({requiredBytes})가 버퍼 범위를 벗어납니다.");
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatUnion
    {
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public int IntValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DoubleUnion
    {
        [FieldOffset(0)] public double DoubleValue;
        [FieldOffset(0)] public long LongValue;
    }
}
