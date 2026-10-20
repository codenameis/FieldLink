using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class MewtocolRequestBuilder
{
    /// <summary>단일 접점 읽기(#RCS) 프레임을 빌드합니다. (사양서 §1.3.1)</summary>
    public static byte[] BuildReadSingleBit(byte station, MewtocolAddress address)
    {
        string body = $"#RCS{address.WireString}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 읽기(#RD) 프레임을 빌드합니다. (사양서 §1.3.7)</summary>
    public static byte[] BuildReadWords(byte station, MewtocolAddress address, ushort wordCount)
    {
        if (wordCount is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~500 사이여야 합니다.");
        }

        int endWord = address.Number + wordCount - 1;
        string body = $"#RD{address.WireString}{address.Number:D5}{endWord:D5}";
        bool useExpanded = wordCount > 27;
        return BuildFrame(station, body, useExpanded);
    }

    /// <summary>단일 접점 쓰기(#WCS) 프레임을 빌드합니다. (사양서 §1.3.4)</summary>
    public static byte[] BuildWriteSingleBit(byte station, MewtocolAddress address, bool bitValue)
    {
        string value = bitValue ? "1" : "0";
        string body = $"#WCS{address.WireString}{value}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 쓰기(#WD) 프레임을 빌드합니다. (사양서 §1.3.8)</summary>
    public static byte[] BuildWriteWords(byte station, MewtocolAddress address, ushort wordCount, byte[] wordData)
    {
        if (wordCount is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~500 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * 2)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        int endWord = address.Number + wordCount - 1;
        var sb = new StringBuilder();
        sb.Append("#WD");
        sb.Append(address.WireString);
        sb.Append(address.Number.ToString("D5"));
        sb.Append(endWord.ToString("D5"));

        for (int i = 0; i < wordData.Length; i++)
        {
            sb.Append(wordData[i].ToString("X2"));
        }

        bool useExpanded = wordCount > 25;
        return BuildFrame(station, sb.ToString(), useExpanded);
    }

    private static byte[] BuildFrame(byte station, string body, bool useExpandedHeader)
    {
        char header = useExpandedHeader ? '<' : '%';
        string stationStr = station.ToString("X2");
        string prefix = $"{header}{stationStr}{body}";

        byte bcc = 0;
        foreach (char c in prefix)
        {
            bcc ^= (byte)c;
        }

        string full = $"{prefix}{bcc:X2}\r";
        return Encoding.ASCII.GetBytes(full);
    }
}
