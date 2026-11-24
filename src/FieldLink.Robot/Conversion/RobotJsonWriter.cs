using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Conversion;

/// <summary>외부 JSON 패키지 없이 스냅샷과 키-값 컬렉션을 직렬화하는 내부 JSON 작성기입니다.</summary>
internal static class RobotJsonWriter
{
    public static string SerializeDoubleArray(IReadOnlyList<double> values)
    {
        var builder = new StringBuilder();
        AppendDoubleArray(builder, values);
        return builder.ToString();
    }

    public static string SerializeStringDictionary(IReadOnlyDictionary<string, string> dictionary)
    {
        var builder = new StringBuilder();
        AppendStringDictionary(builder, dictionary);
        return builder.ToString();
    }

    public static string SerializeStringDictionaryList(IReadOnlyList<IReadOnlyDictionary<string, string>> items)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendStringDictionary(builder, items[i]);
        }

        builder.Append(']');
        return builder.ToString();
    }

    public static void AppendQuotedString(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    public static void AppendDoubleArray(StringBuilder builder, IReadOnlyList<double> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            double value = values[i];
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new RobotException(RobotErrorKind.ParseError, $"JSON으로 직렬화할 수 없는 실수 값입니다: {value}");
            }

            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    public static void AppendSingleArray(StringBuilder builder, IReadOnlyList<float> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            float value = values[i];
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new RobotException(RobotErrorKind.ParseError, $"JSON으로 직렬화할 수 없는 단정밀도 실수 값입니다: {value}");
            }

            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    public static void AppendInt32Array(StringBuilder builder, IReadOnlyList<int> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    public static void AppendBooleanArray(StringBuilder builder, IReadOnlyList<bool> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(values[i] ? "true" : "false");
        }

        builder.Append(']');
    }

    public static void AppendByteArray(StringBuilder builder, IReadOnlyList<byte> values)
    {
        builder.Append('[');
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(values[i].ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
    }

    private static void AppendStringDictionary(StringBuilder builder, IReadOnlyDictionary<string, string> dictionary)
    {
        builder.Append('{');
        bool isFirst = true;
        foreach (KeyValuePair<string, string> pair in dictionary)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            isFirst = false;
            AppendQuotedString(builder, pair.Key);
            builder.Append(':');
            AppendQuotedString(builder, pair.Value);
        }

        builder.Append('}');
    }
}
