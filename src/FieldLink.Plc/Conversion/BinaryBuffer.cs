using System;
using System.Runtime.CompilerServices;

namespace FieldLink.Plc.Conversion;

/// <summary>
/// 바이트 배열에서 다중 바이트 정수(Little/Big Endian)와 16진수 ASCII 문자를
/// 추가 힙 메모리 할당(Zero-Allocation) 없이 읽고 쓰는 저수준 유틸리티입니다.
/// </summary>
internal static class BinaryBuffer
{
    private const string HexAlphabet = "0123456789ABCDEF";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16LE(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16BE(byte[] buffer, int offset) =>
        (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32BE(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24)
        | ((uint)buffer[offset + 1] << 16)
        | ((uint)buffer[offset + 2] << 8)
        | buffer[offset + 3];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16BE(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt24BE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 16);
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32BE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>바이트 값을 2자리 대문자 16진수 ASCII 바이트로 버퍼에 직접 기록합니다.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteAsciiHexByte(byte[] buffer, int offset, byte value)
    {
        buffer[offset] = (byte)HexAlphabet[value >> 4];
        buffer[offset + 1] = (byte)HexAlphabet[value & 0x0F];
    }

    /// <summary>Substring 할당 없이 바이트 배열의 지정된 오프셋에서 16진수 ASCII 문자열을 정수로 파싱합니다.</summary>
    public static int ReadAsciiHex(byte[] buffer, int offset, int characterCount)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || characterCount <= 0 || offset + characterCount > buffer.Length)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"16진수 ASCII 파싱 범위가 버퍼 길이를 벗어났습니다. (Offset: {offset}, Count: {characterCount}, Length: {buffer.Length})");
        }

        int value = 0;
        for (int i = 0; i < characterCount; i++)
        {
            byte ch = buffer[offset + i];
            int digit = ch switch
            {
                >= (byte)'0' and <= (byte)'9' => ch - '0',
                >= (byte)'A' and <= (byte)'F' => ch - 'A' + 10,
                >= (byte)'a' and <= (byte)'f' => ch - 'a' + 10,
                _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 16진수 ASCII 문자입니다. (0x{ch:X2})")
            };

            value = (value << 4) | digit;
        }

        return value;
    }
}
