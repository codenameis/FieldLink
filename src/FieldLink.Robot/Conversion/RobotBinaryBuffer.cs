using System;
using System.Text;

namespace FieldLink.Robot.Conversion;

/// <summary>로봇 프로토콜 패킷 직렬화 및 역직렬화를 위한 바이트 오더·문자열 변환기입니다.</summary>
internal static class RobotBinaryBuffer
{
    public static ushort ReadUInt16LittleEndian(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    public static short ReadInt16LittleEndian(byte[] buffer, int offset) =>
        (short)(buffer[offset] | (buffer[offset + 1] << 8));

    public static ushort ReadUInt16BigEndian(byte[] buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    public static short ReadInt16BigEndian(byte[] buffer, int offset) =>
        (short)((buffer[offset] << 8) | buffer[offset + 1]);

    public static uint ReadUInt32LittleEndian(byte[] buffer, int offset) =>
        buffer[offset]
        | ((uint)buffer[offset + 1] << 8)
        | ((uint)buffer[offset + 2] << 16)
        | ((uint)buffer[offset + 3] << 24);

    public static int ReadInt32LittleEndian(byte[] buffer, int offset) =>
        (int)ReadUInt32LittleEndian(buffer, offset);

    public static float ReadSingleLittleEndian(byte[] buffer, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        byte[] reordered =
        {
            buffer[offset + 3],
            buffer[offset + 2],
            buffer[offset + 1],
            buffer[offset]
        };
        return BitConverter.ToSingle(reordered, 0);
    }

    public static float ReadSingleWordSwapBigEndian(byte[] buffer, int offset)
    {
        // 사양서 03 §6.1: Modbus 워드 순서 CDAB [b0, b1, b2, b3] -> Little-Endian 시스템 입력 [b1, b0, b3, b2]
        if (BitConverter.IsLittleEndian)
        {
            byte[] littleEndianBytes =
            {
                buffer[offset + 1],
                buffer[offset],
                buffer[offset + 3],
                buffer[offset + 2]
            };
            return BitConverter.ToSingle(littleEndianBytes, 0);
        }

        byte[] bigEndianBytes =
        {
            buffer[offset + 2],
            buffer[offset + 3],
            buffer[offset],
            buffer[offset + 1]
        };
        return BitConverter.ToSingle(bigEndianBytes, 0);
    }

    public static double ReadDoubleLittleEndian(byte[] buffer, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            return BitConverter.ToDouble(buffer, offset);
        }

        byte[] reordered =
        {
            buffer[offset + 7],
            buffer[offset + 6],
            buffer[offset + 5],
            buffer[offset + 4],
            buffer[offset + 3],
            buffer[offset + 2],
            buffer[offset + 1],
            buffer[offset]
        };
        return BitConverter.ToDouble(reordered, 0);
    }

    public static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    public static void WriteInt16LittleEndian(byte[] buffer, int offset, short value) =>
        WriteUInt16LittleEndian(buffer, offset, (ushort)value);

    public static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    public static void WriteInt16BigEndian(byte[] buffer, int offset, short value) =>
        WriteUInt16BigEndian(buffer, offset, (ushort)value);

    public static void WriteUInt32LittleEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    public static void WriteInt32LittleEndian(byte[] buffer, int offset, int value) =>
        WriteUInt32LittleEndian(buffer, offset, (uint)value);

    public static void WriteSingleLittleEndian(byte[] buffer, int offset, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Copy(bytes, 0, buffer, offset, 4);
            return;
        }

        buffer[offset] = bytes[3];
        buffer[offset + 1] = bytes[2];
        buffer[offset + 2] = bytes[1];
        buffer[offset + 3] = bytes[0];
    }

    public static void WriteDoubleLittleEndian(byte[] buffer, int offset, double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Copy(bytes, 0, buffer, offset, 8);
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            buffer[offset + i] = bytes[7 - i];
        }
    }

    public static byte[] ReverseByWord(byte[] source, int offset, int length)
    {
        if (length < 0 || length % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "워드 반전 길이는 0 이상의 짝수여야 합니다.");
        }

        byte[] reversed = new byte[length];
        for (int i = 0; i < length; i += 2)
        {
            reversed[i] = source[offset + i + 1];
            reversed[i + 1] = source[offset + i];
        }

        return reversed;
    }

    public static string ReadNullTrimmedString(byte[] buffer, int offset, int length, Encoding encoding)
    {
        string decoded = encoding.GetString(buffer, offset, length);
        int nullIndex = decoded.IndexOf('\0');
        return nullIndex >= 0 ? decoded.Substring(0, nullIndex) : decoded;
    }

    public static string ReadTrimmedAscii(byte[] buffer, int offset, int length)
    {
        string decoded = Encoding.ASCII.GetString(buffer, offset, length);
        return decoded.Trim('\0', ' ', '\t', '\r', '\n');
    }
}
