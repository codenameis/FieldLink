using System;

namespace FieldLink.Plc.Conversion;

public static partial class PlcValueConverter
{
    /// <summary>버퍼에서 16비트 정수 배열을 읽습니다.</summary>
    public static short[] ToInt16Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int16ByteSize);
        short[] result = new short[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToInt16(buffer, offset + (i * Int16ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 16비트 부호 없는 정수 배열을 읽습니다.</summary>
    public static ushort[] ToUInt16Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int16ByteSize);
        ushort[] result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToUInt16(buffer, offset + (i * Int16ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 32비트 정수 배열을 읽습니다.</summary>
    public static int[] ToInt32Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int32ByteSize);
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToInt32(buffer, offset + (i * Int32ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 32비트 부호 없는 정수 배열을 읽습니다.</summary>
    public static uint[] ToUInt32Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int32ByteSize);
        uint[] result = new uint[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToUInt32(buffer, offset + (i * Int32ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 64비트 정수 배열을 읽습니다.</summary>
    public static long[] ToInt64Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int64ByteSize);
        long[] result = new long[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToInt64(buffer, offset + (i * Int64ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 64비트 부호 없는 정수 배열을 읽습니다.</summary>
    public static ulong[] ToUInt64Array(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int64ByteSize);
        ulong[] result = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToUInt64(buffer, offset + (i * Int64ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 단정밀도 부동소수점 배열을 읽습니다.</summary>
    public static float[] ToSingleArray(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int32ByteSize);
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToSingle(buffer, offset + (i * Int32ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 배정밀도 부동소수점 배열을 읽습니다.</summary>
    public static double[] ToDoubleArray(byte[] buffer, int offset, int count, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateArraySlice(buffer, offset, count, Int64ByteSize);
        double[] result = new double[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = ToDouble(buffer, offset + (i * Int64ByteSize), byteOrder);
        }

        return result;
    }

    /// <summary>버퍼에서 1바이트당 1비트 형식으로 불리언 배열을 읽습니다.</summary>
    public static bool[] ToBooleanArray(byte[] buffer, int offset, int count)
    {
        ValidateArraySlice(buffer, offset, count, BooleanByteSize);
        bool[] result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = buffer[offset + i] != 0;
        }

        return result;
    }

    /// <summary>16비트 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(short[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int16ByteSize);

        byte[] result = new byte[values.Length * Int16ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteInt16(result, i * Int16ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>16비트 부호 없는 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(ushort[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int16ByteSize);

        byte[] result = new byte[values.Length * Int16ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteUInt16(result, i * Int16ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>32비트 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(int[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int32ByteSize);

        byte[] result = new byte[values.Length * Int32ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteInt32(result, i * Int32ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>32비트 부호 없는 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(uint[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int32ByteSize);

        byte[] result = new byte[values.Length * Int32ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteUInt32(result, i * Int32ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>64비트 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(long[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int64ByteSize);

        byte[] result = new byte[values.Length * Int64ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteInt64(result, i * Int64ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>64비트 부호 없는 정수 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(ulong[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int64ByteSize);

        byte[] result = new byte[values.Length * Int64ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            WriteUInt64(result, i * Int64ByteSize, values[i], byteOrder);
        }

        return result;
    }

    /// <summary>단정밀도 부동소수점 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(float[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int32ByteSize);

        byte[] result = new byte[values.Length * Int32ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            var union = new FloatUnion { FloatValue = values[i] };
            WriteInt32(result, i * Int32ByteSize, union.IntValue, byteOrder);
        }

        return result;
    }

    /// <summary>배정밀도 부동소수점 배열을 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(double[] values, ByteOrder byteOrder = ByteOrder.BigEndian)
    {
        ValidateSourceArray(values, Int64ByteSize);

        byte[] result = new byte[values.Length * Int64ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            var union = new DoubleUnion { DoubleValue = values[i] };
            WriteInt64(result, i * Int64ByteSize, union.LongValue, byteOrder);
        }

        return result;
    }

    private static void ValidateArraySlice(byte[] buffer, int offset, int count, int elementSize)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 버퍼 길이 이하여야 합니다.");
        }

        if (count < 0 || (long)count * elementSize > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "원소 수가 음수이거나 버퍼 범위를 벗어납니다.");
        }
    }

    private static void ValidateSourceArray<T>(T[] values, int elementSize)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Length > int.MaxValue / elementSize)
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.Length, "배열 바이트 크기가 최대 허용 크기를 초과합니다.");
        }
    }

    /// <summary>불리언 배열을 바이트당 1비트 형식의 바이트 배열로 변환합니다.</summary>
    public static byte[] GetBytes(bool[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        byte[] result = new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = (byte)(values[i] ? 1 : 0);
        }

        return result;
    }
}
