using System;
using System.Text;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 Part A §A.1)</summary>
public static class FujiElectricSpbRequestBuilder
{
    private const string CpuDirect = "FFFF";
    private const int MinWordCount = 1;
    private const int MaxWordCount = 64;
    private const int BytesPerWord = 2;
    private const int FixedWritePayloadBytes = 9;
    private const int FixedHeaderChars = 18;
    private const int AsciiCharsPerByte = 2;
    private const int ByteMask = 0xFF;
    private const string FixedLengthHex = "09";
    private const string CommandReadWords = "0000";
    private const string CommandWriteWords = "0100";
    private const string CommandWriteBit = "0102";

    /// <summary>연속 워드 읽기(명령 00 00) 프레임을 빌드합니다.</summary>
    public static byte[] BuildReadWords(byte station, FujiElectricSpbAddress address, ushort wordCount, bool includeLrc = false)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), $"워드 읽기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        string swappedCount = FujiElectricSpbAddress.FormatSwapped4(wordCount);
        string payload = $"{station:X2}{FixedLengthHex}{CpuDirect}{CommandReadWords}{address.WireWordAddress}{swappedCount}";
        return EncodeFrame(payload, includeLrc);
    }

    /// <summary>연속 워드 쓰기(명령 01 00) 프레임을 빌드합니다.</summary>
    public static byte[] BuildWriteWords(byte station, FujiElectricSpbAddress address, ushort wordCount, byte[] wordData, bool includeLrc = false)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), $"워드 쓰기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        int bodyLen = FixedWritePayloadBytes + (wordCount * BytesPerWord);
        string swappedCount = FujiElectricSpbAddress.FormatSwapped4(wordCount);

        var sb = new StringBuilder(FixedHeaderChars + (wordData.Length * AsciiCharsPerByte));
        sb.Append(station.ToString("X2"));
        sb.Append(bodyLen.ToString("X2"));
        sb.Append(CpuDirect);
        sb.Append(CommandWriteWords);
        sb.Append(address.WireWordAddress);
        sb.Append(swappedCount);

        for (int i = 0; i < wordData.Length; i++)
        {
            sb.Append(wordData[i].ToString("X2"));
        }

        return EncodeFrame(sb.ToString(), includeLrc);
    }

    /// <summary>단일 비트 쓰기(명령 01 02) 프레임을 빌드합니다.</summary>
    public static byte[] BuildWriteBit(byte station, FujiElectricSpbAddress address, bool bitValue, bool includeLrc = false)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string bitAddr = address.BuildBitWriteAddress();
        string valStr = bitValue ? "01" : "00";
        string payload = $"{station:X2}{FixedLengthHex}{CpuDirect}{CommandWriteBit}{bitAddr}{valStr}";
        return EncodeFrame(payload, includeLrc);
    }

    private static byte[] EncodeFrame(string payload, bool includeLrc)
    {
        if (!includeLrc)
        {
            return Encoding.ASCII.GetBytes($":{payload}\r\n");
        }

        int sum = 0;
        for (int i = 0; i < payload.Length; i++)
        {
            sum += (byte)payload[i];
        }

        byte lrc = (byte)((-sum) & ByteMask);
        return Encoding.ASCII.GetBytes($":{payload}{lrc:X2}\r\n");
    }
}
