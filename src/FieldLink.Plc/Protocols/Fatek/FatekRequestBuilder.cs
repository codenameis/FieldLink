using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 요청 프레임을 조립하는 빌더입니다. (사양서 §1)</summary>
public static class FatekRequestBuilder
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

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

        if (bitCount is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "비트 읽기 개수는 1~255 사이여야 합니다.");
        }

        string body = $"44{bitCount:X2}{address.WireString}";
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

        if (wordCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~64 사이여야 합니다.");
        }

        string body = $"46{wordCount:X2}{address.WireString}";
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

        if (bitCount is < 1 or > 255)
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

        var sb = new StringBuilder(10 + bitCount);
        sb.Append("45");
        sb.Append(bitCount.ToString("X2"));
        sb.Append(address.WireString);

        for (int i = 0; i < bitCount; i++)
        {
            sb.Append(bitValues[i] != 0 ? '1' : '0');
        }

        return BuildFrame(station, sb.ToString());
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

        if (wordCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~64 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * 2)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        var sb = new StringBuilder(10 + (wordCount * 4));
        sb.Append("47");
        sb.Append(wordCount.ToString("X2"));
        sb.Append(address.WireString);

        for (int i = 0; i < wordCount; i++)
        {
            byte lo = wordData[i * 2];
            byte hi = wordData[(i * 2) + 1];
            ushort value = (ushort)((hi << 8) | lo);
            sb.Append(value.ToString("X4"));
        }

        return BuildFrame(station, sb.ToString());
    }

    /// <summary>PLC RUN/STOP 제어(명령 41) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <param name="run">RUN 모드로 전환할지 여부입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildRunStop(byte station, bool run) =>
        BuildFrame(station, run ? "411" : "410");

    /// <summary>PLC 상태 읽기(명령 40) 프레임을 빌드합니다.</summary>
    /// <param name="station">PLC 슬레이브 국번(0~255)입니다.</param>
    /// <returns>생성된 ASCII 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadStatus(byte station) =>
        BuildFrame(station, "40");

    private static byte[] BuildFrame(byte station, string body)
    {
        string headerAndBody = $"{station:X2}{body}";
        byte[] asciiBytes = Encoding.ASCII.GetBytes(headerAndBody);

        int sum = Stx;
        for (int i = 0; i < asciiBytes.Length; i++)
        {
            sum += asciiBytes[i];
        }

        byte lrc = (byte)(sum & 0xFF);
        byte[] frame = new byte[1 + asciiBytes.Length + 2 + 1];
        frame[0] = Stx;
        Buffer.BlockCopy(asciiBytes, 0, frame, 1, asciiBytes.Length);
        BinaryBuffer.WriteAsciiHexByte(frame, frame.Length - 3, lrc);
        frame[frame.Length - 1] = Etx;

        return frame;
    }
}
