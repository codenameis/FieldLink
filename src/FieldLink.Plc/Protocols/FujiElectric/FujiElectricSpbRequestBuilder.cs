using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 Part A §A.1)</summary>
public static class FujiElectricSpbRequestBuilder
{
    private const string DirectCpuExtendedStation = "FFFF";
    private const string ReadWordsCommand = "0000";
    private const string WriteWordsCommand = "0100";
    private const string WriteBitCommand = "0102";
    private const string BitOnText = "01";
    private const string BitOffText = "00";

    private const byte MinStation = 1;
    private const byte MaxStation = 255;
    private const ushort MinWordCount = 1;
    private const ushort MaxWordCount = 64;
    private const int FixedReadWordsBodyByteLength = 9;
    private const int FixedWriteBitBodyByteLength = 9;
    private const int WriteWordsBaseBodyByteLength = 9;
    private const int BytesPerWord = 2;
    private const int WriteWordsBaseAsciiLength = 22;
    private const int LowByteMask = 0xFF;

    /// <summary>연속 워드 읽기(명령 00 00) 요청 프레임을 생성합니다.</summary>
    /// <param name="station">대상 PLC 국번(1~255)입니다.</param>
    /// <param name="address">읽기 시작 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 수(1~64)입니다.</param>
    /// <param name="includeLrc">2문자 ASCII LRC 체크섬을 포함할지 여부입니다.</param>
    /// <returns>조립된 ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(
        byte station,
        FujiElectricSpbAddress address,
        ushort wordCount,
        bool includeLrc = false)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("워드 읽기 요청에는 워드 주소만 사용할 수 있습니다.", nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~64 사이여야 합니다.");
        }

        string swappedWordCount = FujiElectricSpbAddress.FormatSwapped4(wordCount);
        string payload = $"{station.ToString("X2", CultureInfo.InvariantCulture)}{FixedReadWordsBodyByteLength.ToString("X2", CultureInfo.InvariantCulture)}{DirectCpuExtendedStation}{ReadWordsCommand}{address.WireWordAddress}{swappedWordCount}";
        return EncodeFrame(payload, includeLrc);
    }

    /// <summary>연속 워드 쓰기(명령 01 00) 요청 프레임을 생성합니다.</summary>
    /// <param name="station">대상 PLC 국번(1~255)입니다.</param>
    /// <param name="address">쓰기 시작 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수(1~64)입니다.</param>
    /// <param name="wordData">리틀엔디언 워드 데이터 바이트 배열입니다.</param>
    /// <param name="includeLrc">2문자 ASCII LRC 체크섬을 포함할지 여부입니다.</param>
    /// <returns>조립된 ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(
        byte station,
        FujiElectricSpbAddress address,
        ushort wordCount,
        byte[] wordData,
        bool includeLrc = false)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("워드 쓰기 요청에는 워드 주소만 사용할 수 있습니다.", nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~64 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        string payload = BuildWriteWordsPayload(station, address, wordCount, wordData);
        return EncodeFrame(payload, includeLrc);
    }

    /// <summary>단일 비트 쓰기(명령 01 02) 요청 프레임을 생성합니다.</summary>
    /// <param name="station">대상 PLC 국번(1~255)입니다.</param>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitValue">기록할 비트 값(true=ON, false=OFF)입니다.</param>
    /// <param name="includeLrc">2문자 ASCII LRC 체크섬을 포함할지 여부입니다.</param>
    /// <returns>조립된 ASCII 요청 바이트 배열입니다.</returns>
    public static byte[] BuildWriteBit(
        byte station,
        FujiElectricSpbAddress address,
        bool bitValue,
        bool includeLrc = false)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException("단일 비트 쓰기 요청에는 비트 주소만 사용할 수 있습니다.", nameof(address));
        }

        string bitWriteAddress = address.BuildBitWriteAddress();
        string bitValueText = bitValue ? BitOnText : BitOffText;
        string payload = $"{station.ToString("X2", CultureInfo.InvariantCulture)}{FixedWriteBitBodyByteLength.ToString("X2", CultureInfo.InvariantCulture)}{DirectCpuExtendedStation}{WriteBitCommand}{bitWriteAddress}{bitValueText}";
        return EncodeFrame(payload, includeLrc);
    }

    private static void ValidateStation(byte station)
    {
        if (station is < MinStation or > MaxStation)
        {
            throw new ArgumentOutOfRangeException(nameof(station), station, $"Fuji SPB 국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
        }
    }

    private static string BuildWriteWordsPayload(
        byte station,
        FujiElectricSpbAddress address,
        ushort wordCount,
        byte[] wordData)
    {
        int bodyByteLength = WriteWordsBaseBodyByteLength + (wordCount * BytesPerWord);
        string swappedWordCount = FujiElectricSpbAddress.FormatSwapped4(wordCount);

        var builder = new StringBuilder(WriteWordsBaseAsciiLength + (wordData.Length * BytesPerWord));
        builder.Append(station.ToString("X2", CultureInfo.InvariantCulture));
        builder.Append(bodyByteLength.ToString("X2", CultureInfo.InvariantCulture));
        builder.Append(DirectCpuExtendedStation);
        builder.Append(WriteWordsCommand);
        builder.Append(address.WireWordAddress);
        builder.Append(swappedWordCount);

        for (int i = 0; i < wordData.Length; i++)
        {
            builder.Append(wordData[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static byte[] EncodeFrame(string payload, bool includeLrc)
    {
        if (!includeLrc)
        {
            return Encoding.ASCII.GetBytes($":{payload}\r\n");
        }

        int asciiByteSum = 0;
        for (int i = 0; i < payload.Length; i++)
        {
            asciiByteSum += (byte)payload[i];
        }

        byte lrc = (byte)((-asciiByteSum) & LowByteMask);
        return Encoding.ASCII.GetBytes($":{payload}{lrc.ToString("X2", CultureInfo.InvariantCulture)}\r\n");
    }
}
