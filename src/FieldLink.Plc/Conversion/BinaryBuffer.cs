using System;
using System.Runtime.CompilerServices;

namespace FieldLink.Plc.Conversion;

/// <summary>
/// 바이트 배열에서 다중 바이트 정수(Little/Big Endian)와 16진수 ASCII 문자를
/// 추가 힙 메모리 할당 없이 읽고 쓰는 내부 변환기입니다.
/// </summary>
internal static class BinaryBuffer
{
    private const string HexAlphabet = "0123456789ABCDEF";
    private const int ByteShift = 8;
    private const int BitShift16 = 16;
    private const int BitShift24 = 24;
    private const int NibbleBitWidth = 4;
    private const int LowNibbleMask = 0x0F;
    private const int MaximumIntHexCharacters = 7;

    /// <summary>버퍼의 지정된 오프셋에서 리틀 엔디언 16비트 부호 없는 정수를 읽습니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16LittleEndian(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << ByteShift));

    /// <summary>버퍼의 지정된 오프셋에서 빅 엔디언 16비트 부호 없는 정수를 읽습니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16BigEndian(byte[] buffer, int offset) =>
        (ushort)((buffer[offset] << ByteShift) | buffer[offset + 1]);

    /// <summary>버퍼의 지정된 오프셋에서 리틀 엔디언 32비트 부호 없는 정수를 읽습니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32LittleEndian(byte[] buffer, int offset) =>
        buffer[offset]
        | ((uint)buffer[offset + 1] << ByteShift)
        | ((uint)buffer[offset + 2] << BitShift16)
        | ((uint)buffer[offset + 3] << BitShift24);

    /// <summary>버퍼의 지정된 오프셋에서 빅 엔디언 32비트 부호 없는 정수를 읽습니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32BigEndian(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << BitShift24)
        | ((uint)buffer[offset + 1] << BitShift16)
        | ((uint)buffer[offset + 2] << ByteShift)
        | buffer[offset + 3];

    /// <summary>버퍼의 지정된 오프셋에 리틀 엔디언 16비트 부호 없는 정수를 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16LittleEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> ByteShift);
    }

    /// <summary>버퍼의 지정된 오프셋에 빅 엔디언 16비트 부호 없는 정수를 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> ByteShift);
        buffer[offset + 1] = (byte)value;
    }

    /// <summary>버퍼의 지정된 오프셋에 빅 엔디언 24비트 부호 없는 정수를 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt24BigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> BitShift16);
        buffer[offset + 1] = (byte)(value >> ByteShift);
        buffer[offset + 2] = (byte)value;
    }

    /// <summary>버퍼의 지정된 오프셋에 리틀 엔디언 32비트 부호 없는 정수를 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32LittleEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> ByteShift);
        buffer[offset + 2] = (byte)(value >> BitShift16);
        buffer[offset + 3] = (byte)(value >> BitShift24);
    }

    /// <summary>버퍼의 지정된 오프셋에 빅 엔디언 32비트 부호 없는 정수를 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> BitShift24);
        buffer[offset + 1] = (byte)(value >> BitShift16);
        buffer[offset + 2] = (byte)(value >> ByteShift);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>바이트 값을 2자리 대문자 16진수 ASCII 바이트로 버퍼에 직접 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteAsciiHexByte(byte[] buffer, int offset, byte value)
    {
        buffer[offset] = (byte)HexAlphabet[value >> NibbleBitWidth];
        buffer[offset + 1] = (byte)HexAlphabet[value & LowNibbleMask];
    }

    /// <summary>문자열 할당 없이 바이트 배열의 지정된 오프셋에서 16진수 ASCII 문자열을 정수로 파싱합니다.</summary>
    public static int ReadAsciiHex(byte[] buffer, int offset, int characterCount)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || characterCount <= 0 || characterCount > MaximumIntHexCharacters || offset > buffer.Length - characterCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"16진수 ASCII 파싱 범위가 버퍼 길이를 벗어났습니다. (Offset: {offset}, Count: {characterCount}, Length: {buffer.Length})");
        }

        int parsedValue = 0;
        for (int i = 0; i < characterCount; i++)
        {
            byte asciiByte = buffer[offset + i];
            if (!AsciiHex.TryParseHexNibble(asciiByte, out int nibbleValue))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 16진수 ASCII 문자입니다. (0x{asciiByte:X2})");
            }

            parsedValue = (parsedValue << NibbleBitWidth) | nibbleValue;
        }

        return parsedValue;
    }
}
