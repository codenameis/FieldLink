using System.Globalization;
using System.Text;

namespace FieldLink.PacketBench.Diagnostics;

/// <summary>프레임 바이트를 화면에 보여 줄 문자열로 바꿉니다.</summary>
internal static class HexText
{
    /// <summary>바이트 배열을 "01 02 AB" 형태로 만듭니다.</summary>
    public static string Format(byte[] frame)
    {
        if (frame.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(frame.Length * 3);
        for (int i = 0; i < frame.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(frame[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>오프셋·16진·ASCII 3단으로 자세히 보여 줍니다.</summary>
    public static string FormatDetailed(byte[] frame)
    {
        if (frame.Length == 0)
        {
            return "(프레임 없음)";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"길이 {frame.Length} 바이트");

        const int BytesPerLine = 16;
        for (int offset = 0; offset < frame.Length; offset += BytesPerLine)
        {
            int count = System.Math.Min(BytesPerLine, frame.Length - offset);
            builder.Append(offset.ToString("X4", CultureInfo.InvariantCulture)).Append("  ");

            for (int i = 0; i < BytesPerLine; i++)
            {
                builder.Append(i < count ? frame[offset + i].ToString("X2", CultureInfo.InvariantCulture) : "  ");
                builder.Append(' ');
            }

            builder.Append(" |");
            for (int i = 0; i < count; i++)
            {
                byte value = frame[offset + i];
                builder.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
            }

            builder.AppendLine("|");
        }

        return builder.ToString().TrimEnd();
    }
}
