using System.Globalization;
using System.Text;

namespace FieldLink.Communication.Tests;

// 기존 Convert의 16진수 API와 배열 범위 문법을 대체한다. 제품 변환기를 사용하지 않아 기대값을 독립적으로 만든다.
internal static class TestBytes
{
    internal static byte[] FromHexString(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length % 2 != 0)
            throw new FormatException("16진수 문자의 수는 짝수여야 합니다.");
        var result = new byte[text.Length / 2];
        for (int i = 0; i < result.Length; i++)
            result[i] = byte.Parse(text.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return result;
    }

    internal static string ToHexString(byte[] data)
    {
        var text = new StringBuilder(data.Length * 2);
        foreach (byte value in data)
            text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    // end는 포함하지 않는 끝 위치, trimEnd는 끝에서 제외할 개수다. 원본의 범위 검사와 복사 의미를 유지한다.
    internal static T[] Slice<T>(T[] values, int start = 0, int? end = null, int trimEnd = 0)
    {
        int stop = end ?? values.Length - trimEnd;
        if (start < 0 || stop < start || stop > values.Length || trimEnd < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        var result = new T[stop - start];
        Array.Copy(values, start, result, 0, result.Length);
        return result;
    }
}
