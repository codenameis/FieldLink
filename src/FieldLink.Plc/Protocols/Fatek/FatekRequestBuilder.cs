using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 요청 프레임을 조립하는 빌더입니다. (사양서 §1)</summary>
public static class FatekRequestBuilder
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>연속 비트 읽기(명령 44) 프레임을 빌드합니다.</summary>
    public static byte[] BuildReadBits(byte station, FatekAddress address, ushort bitCount)
    {
        if (bitCount is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "비트 읽기 개수는 1~255 사이여야 합니다.");
        }

        string countHex = bitCount.ToString("X2");
        string body = $"44{countHex}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 워드 읽기(명령 46) 프레임을 빌드합니다.</summary>
    public static byte[] BuildReadWords(byte station, FatekAddress address, ushort wordCount)
    {
        if (wordCount is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~64 사이여야 합니다.");
        }

        string countHex = wordCount.ToString("X2");
        string body = $"46{countHex}{address.WireString}";
        return BuildFrame(station, body);
    }

    /// <summary>연속 비트 쓰기(명령 45) 프레임을 빌드합니다.</summary>
    public static byte[] BuildWriteBits(byte station, FatekAddress address, ushort bitCount, byte[] bitValues)
    {
        if (bitCount is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "비트 쓰기 개수는 1~255 사이여야 합니다.");
        }
        if (bitValues is null)
        {
            throw new ArgumentNullException(nameof(bitValues));
        }

        var sb = new StringBuilder();
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
    public static byte[] BuildWriteWords(byte station, FatekAddress address, ushort wordCount, byte[] wordData)
    {
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

        var sb = new StringBuilder();
        sb.Append("47");
        sb.Append(wordCount.ToString("X2"));
        sb.Append(address.WireString);

        for (int i = 0; i < wordCount; i++)
        {
            // 리틀엔디언 바이트 [lo, hi] -> 16비트 정수 수치 (High nibble first ASCII hex 4자리)
            byte lo = wordData[i * 2];
            byte hi = wordData[(i * 2) + 1];
            ushort value = (ushort)((hi << 8) | lo);
            sb.Append(value.ToString("X4"));
        }

        return BuildFrame(station, sb.ToString());
    }

    /// <summary>PLC RUN / STOP 제어(명령 41) 프레임을 빌드합니다.</summary>
    public static byte[] BuildRunStop(byte station, bool run)
    {
        string body = run ? "411" : "410";
        return BuildFrame(station, body);
    }

    /// <summary>PLC 시스템 상태 읽기(명령 40) 프레임을 빌드합니다.</summary>
    public static byte[] BuildReadStatus(byte station)
    {
        return BuildFrame(station, "40");
    }

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
        string lrcHex = lrc.ToString("X2");
        byte[] lrcBytes = Encoding.ASCII.GetBytes(lrcHex);

        byte[] frame = new byte[1 + asciiBytes.Length + 2 + 1];
        frame[0] = Stx;
        Buffer.BlockCopy(asciiBytes, 0, frame, 1, asciiBytes.Length);
        frame[frame.Length - 3] = lrcBytes[0];
        frame[frame.Length - 2] = lrcBytes[1];
        frame[frame.Length - 1] = Etx;

        return frame;
    }
}
