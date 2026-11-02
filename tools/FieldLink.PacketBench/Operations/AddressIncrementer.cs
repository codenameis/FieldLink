using System.Globalization;

namespace FieldLink.PacketBench.Operations;

/// <summary>주소 문자열의 끝 숫자를 늘려 다음 주소를 만듭니다.</summary>
/// <remarks>"40001" → "40002", "D100" → "D101", "DB1.DBW10" → "DB1.DBW11" 처럼 동작합니다.</remarks>
internal static class AddressIncrementer
{
    public static string Increment(string address, int offset)
    {
        if (string.IsNullOrWhiteSpace(address) || offset == 0)
        {
            return address;
        }

        int digitStart = address.Length;
        while (digitStart > 0 && char.IsDigit(address[digitStart - 1]))
        {
            digitStart--;
        }

        if (digitStart == address.Length)
        {
            return address;
        }

        string prefix = address[..digitStart];
        string digits = address[digitStart..];

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
        {
            return address;
        }

        long next = number + offset;
        if (next < 0)
        {
            return address;
        }

        string text = next.ToString(CultureInfo.InvariantCulture);
        if (digits.Length > 1 && digits[0] == '0' && text.Length < digits.Length)
        {
            text = text.PadLeft(digits.Length, '0');
        }

        return prefix + text;
    }
}
