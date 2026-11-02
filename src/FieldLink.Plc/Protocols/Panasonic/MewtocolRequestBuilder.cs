using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class MewtocolRequestBuilder
{
    private const char StandardHeader = '%';
    private const char ExpandedHeader = '<';
    private const int MinWordsPerRequest = 1;
    private const int MaxWordsPerRequest = 500;
    private const int StandardReadWordLimit = 27;
    private const int StandardWriteWordLimit = 25;
    private const int BytesPerWord = 2;

    /// <summary>단일 접점 읽기(#RCS) 프레임을 생성합니다. (사양서 §1.3.1)</summary>
    /// <param name="station">대상 PLC 국번(0x01~0x63 또는 0xEE)입니다.</param>
    /// <param name="address">읽을 비트 접점 주소입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadSingleBit(byte station, MewtocolAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string body = $"#RCS{address.WireString}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 읽기(#RD) 프레임을 생성합니다. (사양서 §1.3.7)</summary>
    /// <param name="station">대상 PLC 국번입니다.</param>
    /// <param name="address">읽을 시작 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 수(1~500)입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(byte station, MewtocolAddress address, ushort wordCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount < MinWordsPerRequest || wordCount > MaxWordsPerRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~500 사이여야 합니다.");
        }

        int endWordNumber = address.Number + wordCount - 1;
        string body = $"#RD{address.WireString}{address.Number:D5}{endWordNumber:D5}";
        bool useExpandedHeader = wordCount > StandardReadWordLimit;
        return BuildFrame(station, body, useExpandedHeader);
    }

    /// <summary>단일 접점 쓰기(#WCS) 프레임을 생성합니다. (사양서 §1.3.4)</summary>
    /// <param name="station">대상 PLC 국번입니다.</param>
    /// <param name="address">쓸 비트 접점 주소입니다.</param>
    /// <param name="bitValue">기록할 비트 상태(true: ON, false: OFF)입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteSingleBit(byte station, MewtocolAddress address, bool bitValue)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string valueChar = bitValue ? "1" : "0";
        string body = $"#WCS{address.WireString}{valueChar}";
        return BuildFrame(station, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 쓰기(#WD) 프레임을 생성합니다. (사양서 §1.3.8)</summary>
    /// <param name="station">대상 PLC 국번입니다.</param>
    /// <param name="address">쓸 시작 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수(1~500)입니다.</param>
    /// <param name="wordData">기록할 리틀 엔디언 워드 데이터 바이트 배열입니다. 길이는 <paramref name="wordCount"/> × 2여야 합니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(byte station, MewtocolAddress address, ushort wordCount, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount < MinWordsPerRequest || wordCount > MaxWordsPerRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~500 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        int endWordNumber = address.Number + wordCount - 1;
        var bodyBuilder = new StringBuilder();
        bodyBuilder.Append("#WD");
        bodyBuilder.Append(address.WireString);
        bodyBuilder.Append(address.Number.ToString("D5"));
        bodyBuilder.Append(endWordNumber.ToString("D5"));

        for (int i = 0; i < wordData.Length; i++)
        {
            bodyBuilder.Append(wordData[i].ToString("X2"));
        }

        bool useExpandedHeader = wordCount > StandardWriteWordLimit;
        return BuildFrame(station, bodyBuilder.ToString(), useExpandedHeader);
    }

    private static byte[] BuildFrame(byte station, string body, bool useExpandedHeader)
    {
        char header = useExpandedHeader ? ExpandedHeader : StandardHeader;
        string stationHex = station.ToString("X2");
        string framePrefix = $"{header}{stationHex}{body}";

        byte blockCheckCode = 0;
        foreach (char character in framePrefix)
        {
            blockCheckCode ^= (byte)character;
        }

        string fullFrameText = $"{framePrefix}{blockCheckCode:X2}\r";
        return Encoding.ASCII.GetBytes(fullFrameText);
    }
}
