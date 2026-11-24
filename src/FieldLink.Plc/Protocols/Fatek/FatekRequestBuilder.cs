using System;
using System.Globalization;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 요청 프레임을 조립하는 빌더입니다. (사양서 §1)</summary>
public static class FatekRequestBuilder
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    private const byte MinStation = 1;
    private const byte MaxStation = 254;
    private const int MinBitCount = 1;
    private const int MaxBitCount = 255;
    private const int MinWordCount = 1;
    private const int MaxWordCount = 64;
    private const int MaxDWordCount = MaxWordCount / 2; // 사양서 §1.1: 데이터 본문 500 ASCII 상한 내 32비트 최대 32항목(64워드)
    private const int BytesPerWord = 2;
    private const int HexCharsPerWord = 4;
    private const int BytesPerDWord = 4;
    private const int HexCharsPerDWord = 8;
    private const int BaseBodyHeaderCapacity = 10;
    private const int ChecksumHexLength = 2;
    private const int ChecksumByteMask = 0xFF;
    private const int FrameWrapperLength = 1 + ChecksumHexLength + 1;
    private const char BitTrueDigit = '1';
    private const char BitFalseDigit = '0';

    private const string CommandReadStatus = "40";
    private const string CommandRun = "411";
    private const string CommandStop = "410";
    private const string CommandReadBits = "44";
    private const string CommandWriteBits = "45";
    private const string CommandReadWords = "46";
    private const string CommandWriteWords = "47";

    /// <summary>연속 비트 읽기(명령 44) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitCount">읽을 비트 수입니다. 1~255.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadBits(byte station, FatekAddress address, ushort bitCount)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException("비트 읽기에는 비트 접점 주소만 사용할 수 있습니다.", nameof(address));
        }

        if (bitCount < MinBitCount || bitCount > MaxBitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitCount),
                bitCount,
                $"비트 읽기 개수는 {MinBitCount}~{MaxBitCount} 사이여야 합니다.");
        }

        string body = $"{CommandReadBits}{bitCount.ToString("X2", CultureInfo.InvariantCulture)}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 워드/더블워드 읽기(명령 46) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="address">대상 워드 또는 32비트 레지스터 주소입니다.</param>
    /// <param name="wordCount">읽을 항목 수입니다. 16비트는 1~64, 32비트는 1~32.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(byte station, FatekAddress address, ushort wordCount)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("워드 읽기에는 워드 또는 레지스터 주소만 사용할 수 있습니다.", nameof(address));
        }

        int maxCount = address.Is32Bit ? MaxDWordCount : MaxWordCount;
        if (wordCount < MinWordCount || wordCount > maxCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"{(address.Is32Bit ? "32비트 레지스터" : "워드")} 읽기 개수는 {MinWordCount}~{maxCount} 사이여야 합니다.");
        }

        string body = $"{CommandReadWords}{wordCount.ToString("X2", CultureInfo.InvariantCulture)}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 비트 쓰기(명령 45) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitCount">쓸 비트 수입니다. 1~255.</param>
    /// <param name="bitValues">기록할 비트 값 바이트 배열입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteBits(byte station, FatekAddress address, ushort bitCount, byte[] bitValues)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException("비트 쓰기에는 비트 접점 주소만 사용할 수 있습니다.", nameof(address));
        }

        if (bitCount < MinBitCount || bitCount > MaxBitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitCount),
                bitCount,
                $"비트 쓰기 개수는 {MinBitCount}~{MaxBitCount} 사이여야 합니다.");
        }

        if (bitValues is null)
        {
            throw new ArgumentNullException(nameof(bitValues));
        }

        if (bitValues.Length != bitCount)
        {
            throw new ArgumentException($"비트 데이터 배열 길이({bitValues.Length})가 요청 개수({bitCount})와 일치하지 않습니다.", nameof(bitValues));
        }

        var bodyBuilder = new StringBuilder(BaseBodyHeaderCapacity + bitCount);
        bodyBuilder.Append(CommandWriteBits);
        bodyBuilder.Append(bitCount.ToString("X2", CultureInfo.InvariantCulture));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < bitCount; i++)
        {
            bodyBuilder.Append(bitValues[i] != 0 ? BitTrueDigit : BitFalseDigit);
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>연속 16비트 워드 쓰기(명령 47) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="address">대상 16비트 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수입니다. 1~64.</param>
    /// <param name="wordData">기록할 워드 데이터 바이트 배열(리틀 엔디언)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(byte station, FatekAddress address, ushort wordCount, byte[] wordData)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit || address.Is32Bit)
        {
            throw new ArgumentException("16비트 워드 쓰기에는 16비트 레지스터 주소만 사용할 수 있습니다.", nameof(address));
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

        var bodyBuilder = new StringBuilder(BaseBodyHeaderCapacity + (wordCount * HexCharsPerWord));
        bodyBuilder.Append(CommandWriteWords);
        bodyBuilder.Append(wordCount.ToString("X2", CultureInfo.InvariantCulture));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < wordCount; i++)
        {
            int byteOffset = i * BytesPerWord;
            ushort wordValue = BinaryBuffer.ReadUInt16LittleEndian(wordData, byteOffset);
            bodyBuilder.Append(wordValue.ToString("X4", CultureInfo.InvariantCulture));
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>연속 32비트 레지스터(DR/DD/DF/DRT/DRC/DW*) 쓰기(명령 47) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="address">대상 32비트 레지스터 주소입니다.</param>
    /// <param name="dwordCount">쓸 32비트 레지스터 수입니다. 1~32.</param>
    /// <param name="dwordData">기록할 32비트 데이터 바이트 배열(리틀 엔디언)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteDWords(byte station, FatekAddress address, ushort dwordCount, byte[] dwordData)
    {
        ValidateStation(station);
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit || !address.Is32Bit)
        {
            throw new ArgumentException("32비트 쓰기에는 32비트 레지스터 주소만 사용할 수 있습니다.", nameof(address));
        }

        if (dwordCount < MinWordCount || dwordCount > MaxDWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dwordCount),
                dwordCount,
                $"32비트 레지스터 쓰기 개수는 {MinWordCount}~{MaxDWordCount} 사이여야 합니다.");
        }

        if (dwordData is null)
        {
            throw new ArgumentNullException(nameof(dwordData));
        }

        if (dwordData.Length != dwordCount * BytesPerDWord)
        {
            throw new ArgumentException("데이터 길이가 32비트 레지스터 개수와 일치하지 않습니다.", nameof(dwordData));
        }

        var bodyBuilder = new StringBuilder(BaseBodyHeaderCapacity + (dwordCount * HexCharsPerDWord));
        bodyBuilder.Append(CommandWriteWords);
        bodyBuilder.Append(dwordCount.ToString("X2", CultureInfo.InvariantCulture));
        bodyBuilder.Append(address.WireString);

        for (int i = 0; i < dwordCount; i++)
        {
            int byteOffset = i * BytesPerDWord;
            uint dwordValue = BinaryBuffer.ReadUInt32LittleEndian(dwordData, byteOffset);
            bodyBuilder.Append(dwordValue.ToString("X8", CultureInfo.InvariantCulture));
        }

        return BuildFrame(station, bodyBuilder.ToString());
    }

    /// <summary>PLC RUN/STOP 제어(명령 41) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <param name="run">RUN 모드로 전환할지 여부입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildRunStop(byte station, bool run)
    {
        ValidateStation(station);
        return BuildFrame(station, run ? CommandRun : CommandStop);
    }

    /// <summary>PLC 상태 읽기(명령 40) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(1~254)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadStatus(byte station)
    {
        ValidateStation(station);
        return BuildFrame(station, CommandReadStatus);
    }

    private static void ValidateStation(byte station)
    {
        if (station is < MinStation or > MaxStation)
        {
            throw new ArgumentOutOfRangeException(nameof(station), station, $"FATEK 국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
        }
    }

    private static byte[] BuildFrame(byte station, string commandAndData)
    {
        string content = $"{station.ToString("X2", CultureInfo.InvariantCulture)}{commandAndData}";
        byte[] asciiBytes = Encoding.ASCII.GetBytes(content);

        int checksumTotal = Stx;
        for (int i = 0; i < asciiBytes.Length; i++)
        {
            checksumTotal += asciiBytes[i];
        }

        byte lrc = (byte)(checksumTotal & ChecksumByteMask);
        byte[] frame = new byte[asciiBytes.Length + FrameWrapperLength];
        frame[0] = Stx;
        Buffer.BlockCopy(asciiBytes, 0, frame, 1, asciiBytes.Length);
        BinaryBuffer.WriteAsciiHexByte(frame, frame.Length - (ChecksumHexLength + 1), lrc);
        frame[frame.Length - 1] = Etx;

        return frame;
    }
}
