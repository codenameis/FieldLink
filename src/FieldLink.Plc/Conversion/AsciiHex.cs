using System;

namespace FieldLink.Plc.Conversion;

/// <summary>16진수 문자열과 바이트 배열 간의 상호 변환을 지원하는 순수 함수 모음입니다.</summary>
public static class AsciiHex
{
    private const int NibbleBitWidth = 4;
    private const int LowNibbleMask = 0x0F;
    private const int CharactersPerByte = 2;
    private const int HexDigitOffset = 10;
    private static readonly char[] HexCharactersUpper = "0123456789ABCDEF".ToCharArray();

    /// <summary>16진수 문자열을 바이트 배열로 변환합니다. 공백은 무시합니다.</summary>
    /// <param name="hex">16진수 문자열입니다. (예: "0103006B")</param>
    /// <returns>변환된 바이트 배열입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/>가 null일 때.</exception>
    /// <exception cref="FormatException">문자열 길이가 홀수이거나 16진수 문자가 아닐 때.</exception>
    public static byte[] GetBytes(string hex)
    {
        if (hex is null)
        {
            throw new ArgumentNullException(nameof(hex));
        }

        string sanitized = hex.Replace(" ", string.Empty);
        if (sanitized.Length % CharactersPerByte != 0)
        {
            throw new FormatException("16진수 문자열 길이는 짝수여야 합니다.");
        }

        byte[] bytes = new byte[sanitized.Length / CharactersPerByte];
        for (int i = 0; i < bytes.Length; i++)
        {
            int highNibble = ParseHexNibble(sanitized[i * CharactersPerByte]);
            int lowNibble = ParseHexNibble(sanitized[(i * CharactersPerByte) + 1]);
            bytes[i] = (byte)((highNibble << NibbleBitWidth) | lowNibble);
        }

        return bytes;
    }

    /// <summary>바이트 배열의 특정 영역을 대문자 16진수 문자열로 변환합니다.</summary>
    /// <param name="buffer">바이트 배열입니다.</param>
    /// <param name="offset">변환 시작 위치입니다.</param>
    /// <param name="count">변환할 바이트 수입니다.</param>
    /// <returns>대문자 16진수 문자열입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">오프셋 또는 바이트 수가 유효 범위를 벗어났을 때.</exception>
    public static string GetString(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 배열 크기 이하여야 합니다.");
        }

        if (count < 0 || count > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "바이트 수는 0 이상이고 배열 범위를 초과할 수 없습니다.");
        }

        if (count > int.MaxValue / CharactersPerByte)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "변환할 문자열 길이가 최대 허용 크기를 초과합니다.");
        }

        char[] chars = new char[count * CharactersPerByte];
        for (int i = 0; i < count; i++)
        {
            byte value = buffer[offset + i];
            chars[i * CharactersPerByte] = HexCharactersUpper[value >> NibbleBitWidth];
            chars[(i * CharactersPerByte) + 1] = HexCharactersUpper[value & LowNibbleMask];
        }

        return new string(chars);
    }

    /// <summary>바이트 배열 전체를 대문자 16진수 문자열로 변환합니다.</summary>
    /// <param name="buffer">바이트 배열입니다.</param>
    /// <returns>대문자 16진수 문자열입니다.</returns>
    public static string GetString(byte[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return GetString(buffer, 0, buffer.Length);
    }

    internal static bool TryParseHexNibble(int characterCode, out int nibbleValue)
    {
        if (characterCode is >= '0' and <= '9')
        {
            nibbleValue = characterCode - '0';
            return true;
        }

        if (characterCode is >= 'A' and <= 'F')
        {
            nibbleValue = characterCode - 'A' + HexDigitOffset;
            return true;
        }

        if (characterCode is >= 'a' and <= 'f')
        {
            nibbleValue = characterCode - 'a' + HexDigitOffset;
            return true;
        }

        nibbleValue = 0;
        return false;
    }

    private static int ParseHexNibble(char character)
    {
        if (!TryParseHexNibble(character, out int nibbleValue))
        {
            throw new FormatException($"유효하지 않은 16진수 문자 '{character}'입니다.");
        }

        return nibbleValue;
    }
}
