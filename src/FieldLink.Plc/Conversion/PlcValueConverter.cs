using System;
using System.Runtime.InteropServices;

namespace FieldLink.Plc.Conversion;

/// <summary>바이트 순서(ByteOrder)를 고려하여 PLC 바이트와 .NET 기본 형식 간 상호 변환을 수행하는 변환기입니다.</summary>
public static partial class PlcValueConverter
{
    /// <summary>버퍼에서 16비트 부호 있는 정수를 읽습니다.</summary>
    public static short ToInt16(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        return (short)ToUInt16(buffer, offset, byteOrder);
    }

    /// <summary>버퍼에서 16비트 부호 없는 정수를 읽습니다.</summary>
    public static ushort ToUInt16(byte[] buffer, int offset = 0, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateBuffer(buffer, offset, 2);
        byte b0 = buffer[offset];
        byte b1 = buffer[offset + 1];

        return byteOrder is ByteOrder.LittleEndian or ByteOrder.LittleEndianWordSwap
            ? (ushort)(b0 | (b1 << 8))
            : (ushort)((b0 << 8) | b1);
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
        byte b0 = buffer[offset];
        byte b1 = buffer[offset + 1];
        byte b2 = buffer[offset + 2];
        byte b3 = buffer[offset + 3];

        return byteOrder switch
        {
            ByteOrder.BigEndian => ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3,
            ByteOrder.BigEndianWordSwap => ((uint)b2 << 24) | ((uint)b3 << 16) | ((uint)b0 << 8) | b1,
            ByteOrder.LittleEndian => ((uint)b3 << 24) | ((uint)b2 << 16) | ((uint)b1 << 8) | b0,
            ByteOrder.LittleEndianWordSwap => ((uint)b1 << 24) | ((uint)b0 << 16) | ((uint)b3 << 8) | b2,
            _ => throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "지원하지 않는 바이트 순서입니다.")
        };
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
        byte b0 = buffer[offset];
        byte b1 = buffer[offset + 1];
        byte b2 = buffer[offset + 2];
        byte b3 = buffer[offset + 3];
        byte b4 = buffer[offset + 4];
        byte b5 = buffer[offset + 5];
        byte b6 = buffer[offset + 6];
        byte b7 = buffer[offset + 7];

        return byteOrder switch
        {
            ByteOrder.BigEndian =>
                ((ulong)b0 << 56) | ((ulong)b1 << 48) | ((ulong)b2 << 40) | ((ulong)b3 << 32) |
                ((ulong)b4 << 24) | ((ulong)b5 << 16) | ((ulong)b6 << 8) | b7,
            ByteOrder.BigEndianWordSwap =>
                ((ulong)b6 << 56) | ((ulong)b7 << 48) | ((ulong)b4 << 40) | ((ulong)b5 << 32) |
                ((ulong)b2 << 24) | ((ulong)b3 << 16) | ((ulong)b0 << 8) | b1,
            ByteOrder.LittleEndian =>
                ((ulong)b7 << 56) | ((ulong)b6 << 48) | ((ulong)b5 << 40) | ((ulong)b4 << 32) |
                ((ulong)b3 << 24) | ((ulong)b2 << 16) | ((ulong)b1 << 8) | b0,
            ByteOrder.LittleEndianWordSwap =>
                ((ulong)b1 << 56) | ((ulong)b0 << 48) | ((ulong)b3 << 40) | ((ulong)b2 << 32) |
                ((ulong)b5 << 24) | ((ulong)b4 << 16) | ((ulong)b7 << 8) | b6,
            _ => throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "지원하지 않는 바이트 순서입니다.")
        };
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
        ValidateBuffer(buffer, offset, 1);
        if (bitIndex is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), bitIndex, "비트 인덱스는 0에서 7 사이여야 합니다.");
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
        byte b0 = (byte)((value >> 8) & 0xFF);
        byte b1 = (byte)(value & 0xFF);

        if (byteOrder is ByteOrder.LittleEndian or ByteOrder.LittleEndianWordSwap)
        {
            destination[offset] = b1;
            destination[offset + 1] = b0;
        }
        else
        {
            destination[offset] = b0;
            destination[offset + 1] = b1;
        }
    }

    internal static void WriteInt32(byte[] destination, int offset, int value, ByteOrder byteOrder)
    {
        WriteUInt32(destination, offset, (uint)value, byteOrder);
    }

    internal static void WriteUInt32(byte[] destination, int offset, uint value, ByteOrder byteOrder)
    {
        byte a = (byte)((value >> 24) & 0xFF);
        byte b = (byte)((value >> 16) & 0xFF);
        byte c = (byte)((value >> 8) & 0xFF);
        byte d = (byte)(value & 0xFF);

        (destination[offset], destination[offset + 1], destination[offset + 2], destination[offset + 3]) = byteOrder switch
        {
            ByteOrder.BigEndian => (a, b, c, d),
            ByteOrder.BigEndianWordSwap => (c, d, a, b),
            ByteOrder.LittleEndian => (d, c, b, a),
            ByteOrder.LittleEndianWordSwap => (b, a, d, c),
            _ => throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "지원하지 않는 바이트 순서입니다.")
        };
    }

    internal static void WriteInt64(byte[] destination, int offset, long value, ByteOrder byteOrder)
    {
        WriteUInt64(destination, offset, (ulong)value, byteOrder);
    }

    internal static void WriteUInt64(byte[] destination, int offset, ulong value, ByteOrder byteOrder)
    {
        byte a = (byte)((value >> 56) & 0xFF);
        byte b = (byte)((value >> 48) & 0xFF);
        byte c = (byte)((value >> 40) & 0xFF);
        byte d = (byte)((value >> 32) & 0xFF);
        byte e = (byte)((value >> 24) & 0xFF);
        byte f = (byte)((value >> 16) & 0xFF);
        byte g = (byte)((value >> 8) & 0xFF);
        byte h = (byte)(value & 0xFF);

        byte[] ordered = byteOrder switch
        {
            ByteOrder.BigEndian => new[] { a, b, c, d, e, f, g, h },
            ByteOrder.BigEndianWordSwap => new[] { g, h, e, f, c, d, a, b },
            ByteOrder.LittleEndian => new[] { h, g, f, e, d, c, b, a },
            ByteOrder.LittleEndianWordSwap => new[] { b, a, d, c, f, e, h, g },
            _ => throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "지원하지 않는 바이트 순서입니다.")
        };

        Array.Copy(ordered, 0, destination, offset, 8);
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

        if (offset + requiredBytes > buffer.Length)
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
