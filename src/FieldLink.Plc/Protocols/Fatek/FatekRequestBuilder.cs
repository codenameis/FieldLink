using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 요청 프레임을 조립하는 빌더입니다. (사양서 §1)</summary>
public static class FatekRequestBuilder
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    private const int MinBitCount = 1;
    private const int MaxBitCount = 255;
    private const int MinWordCount = 1;
    private const int MaxWordCount = 64;
    private const int BytesPerWord = 2;
    private const int HexCharsPerWord = 4;
    private const int BytesPerDWord = 4;
    private const int HexCharsPerDWord = 8;
    private const int ChecksumHexLength = 2;

    private const string CommandReadStatus = "40";
    private const string CommandRun = "411";
    private const string CommandStop = "410";
    private const string CommandReadBits = "44";
    private const string CommandWriteBits = "45";
    private const string CommandReadWords = "46";
    private const string CommandWriteWords = "47";

    /// <summary>연속 비트 읽기(명령 44) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitCount">읽을 비트 수입니다. 1~255.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadBits(byte station, FatekAddress address, ushort bitCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (bitCount is < MinBitCount or > MaxBitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "비트 읽기 개수는 1~255 사이여야 합니다.");
        }

        string body = $"{CommandReadBits}{bitCount:X2}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 워드 읽기(명령 46) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="address">대상 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 수입니다. 1~64.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(byte station, FatekAddress address, ushort wordCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~64 사이여야 합니다.");
        }

        string body = $"{CommandReadWords}{wordCount:X2}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 비트 쓰기(명령 45) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitCount">쓸 비트 수입니다. 1~255.</param>
    /// <param name="bitValues">기록할 비트 값 바이트 배열입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteBits(byte station, FatekAddress address, ushort bitCount, byte[] bitValues)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (bitCount is < MinBitCount or > MaxBitCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "비트 쓰기 개수는 1~255 사이여야 합니다.");
        }

        if (bitValues is null)
        {
            throw new ArgumentNullException(nameof(bitValues));
        }

        if (bitValues.Length < bitCount)
        {
            throw new ArgumentException("비트 데이터 배열 길이가 요청 개수보다 작습니다.", nameof(bitValues));
        }

        var bodyBuilder = new StringBuilder(10 + bitCount);
        bodyBuilder.Append(CommandWriteBits);
        bodyBuilder.Append(bitCount.ToString("X2"));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < bitCount; i++)
        {
            bodyBuilder.Append(bitValues[i] != 0 ? '1' : '0');
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>연속 워드 쓰기(명령 47) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="address">대상 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수입니다. 1~64.</param>
    /// <param name="wordData">기록할 워드 데이터 바이트 배열(리틀 엔디언)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(byte station, FatekAddress address, ushort wordCount, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
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

        var bodyBuilder = new StringBuilder(10 + (wordCount * HexCharsPerWord));
        bodyBuilder.Append(CommandWriteWords);
        bodyBuilder.Append(wordCount.ToString("X2"));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < wordCount; i++)
        {
            int byteOffset = i * BytesPerWord;
            ushort wordValue = BinaryBuffer.ReadUInt16LittleEndian(wordData, byteOffset);
            bodyBuilder.Append(wordValue.ToString("X4"));
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>연속 32비트 레지스터(DRC) 쓰기(명령 47) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="address">대상 32비트 레지스터 주소입니다.</param>
    /// <param name="dwordCount">쓸 32비트 레지스터 수입니다. 1~64.</param>
    /// <param name="dwordData">기록할 32비트 데이터 바이트 배열(리틀 엔디언)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteDWords(byte station, FatekAddress address, ushort dwordCount, byte[] dwordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (dwordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(dwordCount), "32비트 레지스터 쓰기 개수는 1~64 사이여야 합니다.");
        }

        if (dwordData is null)
        {
            throw new ArgumentNullException(nameof(dwordData));
        }

        if (dwordData.Length != dwordCount * BytesPerDWord)
        {
            throw new ArgumentException("데이터 길이가 32비트 레지스터 개수와 일치하지 않습니다.", nameof(dwordData));
        }

        var bodyBuilder = new StringBuilder(10 + (dwordCount * HexCharsPerDWord));
        bodyBuilder.Append(CommandWriteWords);
        bodyBuilder.Append(dwordCount.ToString("X2"));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < dwordCount; i++)
        {
            int byteOffset = i * BytesPerDWord;
            uint dwordValue = BinaryBuffer.ReadUInt32LittleEndian(dwordData, byteOffset);
            bodyBuilder.Append(dwordValue.ToString("X8"));
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>PLC RUN/STOP 제어(명령 41) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="run">RUN 모드로 전환할지 여부입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildRunStop(byte station, bool run) =>
        BuildFrame(station, run ? CommandRun : CommandStop);

    /// <summary>PLC 상태 읽기(명령 40) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadStatus(byte station) =>
        BuildFrame(station, CommandReadStatus);

    private static byte[] BuildFrame(byte station, string body)
    {
        string headerAndBody = $"{station:X2}{body}";
        byte[] asciiBytes = Encoding.ASCII.GetBytes(headerAndBody);

        int checksumTotal = Stx;
        for (int i = 0; i < asciiBytes.Length; i++)
        {
            checksumTotal += asciiBytes[i];
        }

        byte lrc = (byte)(checksumTotal & 0xFF);
        byte[] frame = new byte[1 + asciiBytes.Length + ChecksumHexLength + 1];
        frame[0] = Stx;
        Buffer.BlockCopy(asciiBytes, 0, frame, 1, asciiBytes.Length);
        BinaryBuffer.WriteAsciiHexByte(frame, frame.Length - (ChecksumHexLength + 1), lrc);
        frame[frame.Length - 1] = Etx;

        return frame;
    }
}
