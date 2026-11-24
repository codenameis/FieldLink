using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class MewtocolRequestBuilder
{
    private const int MinWordCount = 1;
    private const int MaxWordCount = 500;
    private const int MaxWordNumber = 99999;
    private const int BytesPerWord = 2;
    private const int StandardReadWordLimit = 27;
    private const int StandardWriteWordLimit = 25;
    private const char StandardHeader = '%';
    private const char ExpandedHeader = '<';
    private const string BitOnText = "1";
    private const string BitOffText = "0";
    private const char FrameTerminator = '\r';

    /// <summary>단일 접점 읽기(#RCS) 프레임을 빌드합니다. (사양서 §1.3.1)</summary>
    public static byte[] BuildReadSingleBit(byte station, MewtocolAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        string body = $"#RCS{address.WireString}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 읽기(#RD) 프레임을 빌드합니다. (사양서 §1.3.7)</summary>
    public static byte[] BuildReadWords(byte station, MewtocolAddress address, ushort wordCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (wordCount < MinWordCount || wordCount > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"워드 읽기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        int endWord = checked(address.Number + wordCount - 1);
        if (endWord > MaxWordNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), endWord, $"종료 워드 번호({endWord})가 최대 범위({MaxWordNumber})를 초과합니다.");
        }

        string body = $"#RD{address.WireString}{address.Number:D5}{endWord:D5}";
        bool useExpanded = wordCount > StandardReadWordLimit;
        return BuildFrame(station, body, useExpanded);
    }

    /// <summary>단일 접점 쓰기(#WCS) 프레임을 빌드합니다. (사양서 §1.3.4)</summary>
    public static byte[] BuildWriteSingleBit(byte station, MewtocolAddress address, bool bitValue)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        string value = bitValue ? BitOnText : BitOffText;
        string body = $"#WCS{address.WireString}{value}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 쓰기(#WD) 프레임을 빌드합니다. (사양서 §1.3.8)</summary>
    public static byte[] BuildWriteWords(byte station, MewtocolAddress address, ushort wordCount, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (wordCount < MinWordCount || wordCount > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"워드 쓰기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        int endWord = checked(address.Number + wordCount - 1);
        if (endWord > MaxWordNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), endWord, $"종료 워드 번호({endWord})가 최대 범위({MaxWordNumber})를 초과합니다.");
        }

        var sb = new StringBuilder();
        sb.Append("#WD");
        sb.Append(address.WireString);
        sb.Append(address.Number.ToString("D5"));
        sb.Append(endWord.ToString("D5"));

        for (int i = 0; i < wordData.Length; i++)
        {
            sb.Append(wordData[i].ToString("X2"));
        }

        bool useExpanded = wordCount > StandardWriteWordLimit;
        return BuildFrame(station, sb.ToString(), useExpanded);
    }

    private static byte[] BuildFrame(byte station, string body, bool useExpandedHeader)
    {
        char header = useExpandedHeader ? ExpandedHeader : StandardHeader;
        string stationStr = station.ToString("X2");
        string prefix = $"{header}{stationStr}{body}";

        byte bcc = 0;
        foreach (char c in prefix)
        {
            bcc ^= (byte)c;
        }

        string full = $"{prefix}{bcc:X2}{FrameTerminator}";
        return Encoding.ASCII.GetBytes(full);
    }
}
