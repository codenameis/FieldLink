using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class MewtocolRequestBuilder
{
    private const char StandardHeader = '%';
    private const char ExpandedHeader = '<';
    private const byte MinStationNumber = 1;
    private const byte MaxStationNumber = 99;
    private const byte DirectConnectionStation = 0xEE;
    private const string DirectConnectionStationText = "EE";
    private const int MinWordsPerRequest = 1;
    private const int MaxWordsPerRequest = 500;
    private const int MaxWordNumber = 99999;
    private const int StandardReadWordLimit = 27;
    private const int StandardWriteWordLimit = 24;
    private const int BytesPerWord = 2;

    /// <summary>단일 접점 읽기(#RCS) 프레임을 생성합니다. (사양서 §1.3.1)</summary>
    /// <param name="station">대상 PLC 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="address">읽을 비트 접점 주소입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadSingleBit(byte station, MewtocolAddress address)
    {
        string stationText = FormatStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        string body = $"#RCS{address.WireString}";
        return BuildFrame(stationText, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 읽기(#RD) 프레임을 생성합니다. (사양서 §1.3.7)</summary>
    /// <param name="station">대상 PLC 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="address">읽을 시작 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 수(1~500)입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(byte station, MewtocolAddress address, ushort wordCount)
    {
        string stationText = FormatStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (wordCount < MinWordsPerRequest || wordCount > MaxWordsPerRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~500 사이여야 합니다.");
        }

        int endWordNumber = checked(address.Number + wordCount - 1);
        if (endWordNumber > MaxWordNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), endWordNumber, $"종료 워드 번호({endWordNumber})가 최대 범위({MaxWordNumber})를 초과합니다.");
        }

        string body = string.Format(
            CultureInfo.InvariantCulture,
            "#RD{0}{1:D5}{2:D5}",
            address.WireString,
            address.Number,
            endWordNumber);

        bool useExpandedHeader = wordCount > StandardReadWordLimit;
        return BuildFrame(stationText, body, useExpandedHeader);
    }

    /// <summary>단일 접점 쓰기(#WCS) 프레임을 생성합니다. (사양서 §1.3.4)</summary>
    /// <param name="station">대상 PLC 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="address">쓸 비트 접점 주소입니다.</param>
    /// <param name="bitValue">기록할 비트 상태(true: ON, false: OFF)입니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteSingleBit(byte station, MewtocolAddress address, bool bitValue)
    {
        string stationText = FormatStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        string valueChar = bitValue ? "1" : "0";
        string body = $"#WCS{address.WireString}{valueChar}";
        return BuildFrame(stationText, body, useExpandedHeader: false);
    }

    /// <summary>데이터 영역 연속 쓰기(#WD) 프레임을 생성합니다. (사양서 §1.3.8)</summary>
    /// <param name="station">대상 PLC 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="address">쓸 시작 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수(1~500)입니다.</param>
    /// <param name="wordData">기록할 리틀 엔디언 워드 데이터 바이트 배열입니다. 길이는 <paramref name="wordCount"/> × 2여야 합니다.</param>
    /// <returns>생성된 MEWTOCOL-COM ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(byte station, MewtocolAddress address, ushort wordCount, byte[] wordData)
    {
        string stationText = FormatStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
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

        int endWordNumber = checked(address.Number + wordCount - 1);
        if (endWordNumber > MaxWordNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), endWordNumber, $"종료 워드 번호({endWordNumber})가 최대 범위({MaxWordNumber})를 초과합니다.");
        }

        var bodyBuilder = new StringBuilder();
        bodyBuilder.Append("#WD");
        bodyBuilder.Append(address.WireString);
        bodyBuilder.Append(address.Number.ToString("D5", CultureInfo.InvariantCulture));
        bodyBuilder.Append(endWordNumber.ToString("D5", CultureInfo.InvariantCulture));

        for (int i = 0; i < wordData.Length; i++)
        {
            bodyBuilder.Append(wordData[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        bool useExpandedHeader = wordCount > StandardWriteWordLimit;
        return BuildFrame(stationText, bodyBuilder.ToString(), useExpandedHeader);
    }

    internal static string FormatStation(byte station)
    {
        if (station == DirectConnectionStation)
        {
            return DirectConnectionStationText;
        }

        if (station < MinStationNumber || station > MaxStationNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(station),
                station,
                $"국번은 {MinStationNumber}~{MaxStationNumber} 또는 0x{DirectConnectionStation:X2}여야 합니다.");
        }

        return station.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static byte[] BuildFrame(string stationText, string body, bool useExpandedHeader)
    {
        char header = useExpandedHeader ? ExpandedHeader : StandardHeader;
        string framePrefix = $"{header}{stationText}{body}";

        byte blockCheckCode = 0;
        foreach (char character in framePrefix)
        {
            blockCheckCode ^= (byte)character;
        }

        string bccHex = blockCheckCode.ToString("X2", CultureInfo.InvariantCulture);
        string fullFrameText = $"{framePrefix}{bccHex}\r";
        return Encoding.ASCII.GetBytes(fullFrameText);
    }
}
