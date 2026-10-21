using System;
using System.Globalization;

namespace FieldLink.PacketBench.Operations;

/// <summary>입력한 값 문자열을 자료형에 맞게 해석합니다. 16진수는 0x 접두사를 씁니다.</summary>
internal static class ValueTextParser
{
    public static bool TryParseBit(string text, out bool value)
    {
        string trimmed = text.Trim();

        if (bool.TryParse(trimmed, out value))
        {
            return true;
        }

        if (trimmed == "1" || string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (trimmed == "0" || string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    public static bool TryParseInt16(string text, out short value)
    {
        value = 0;
        if (!TryParseSigned(text, out long raw) || raw is < short.MinValue or > short.MaxValue)
        {
            return false;
        }

        value = (short)raw;
        return true;
    }

    public static bool TryParseUInt16(string text, out ushort value)
    {
        value = 0;
        if (!TryParseUnsigned(text, out ulong raw) || raw > ushort.MaxValue)
        {
            return false;
        }

        value = (ushort)raw;
        return true;
    }

    public static bool TryParseInt32(string text, out int value)
    {
        value = 0;
        if (!TryParseSigned(text, out long raw) || raw is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        value = (int)raw;
        return true;
    }

    public static bool TryParseUInt32(string text, out uint value)
    {
        value = 0;
        if (!TryParseUnsigned(text, out ulong raw) || raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    public static bool TryParseSingle(string text, out float value)
        => float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static bool TryParseDouble(string text, out double value)
        => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseSigned(string text, out long value)
    {
        string trimmed = text.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = 0;
            if (!ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hex))
            {
                return false;
            }

            value = unchecked((long)hex);
            return true;
        }

        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseUnsigned(string text, out ulong value)
    {
        string trimmed = text.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
