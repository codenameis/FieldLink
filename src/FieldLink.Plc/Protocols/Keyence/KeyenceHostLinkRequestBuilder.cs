using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 Part A §A.1)</summary>
public static class KeyenceHostLinkRequestBuilder
{
    /// <summary>통신 개시(CR) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildConnect(byte? station)
    {
        string command = station.HasValue ? $"CR {station.Value:D2}\r" : "CR\r";
        return Encoding.ASCII.GetBytes(command);
    }

    /// <summary>통신 종료(CQ) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildDisconnect() => Encoding.ASCII.GetBytes("CQ\r");

    /// <summary>연속 읽기(RDS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildRead(KeyenceHostLinkAddress address, ushort count)
    {
        string command = $"RDS {address.WireString} {count}\r";
        return Encoding.ASCII.GetBytes(command);
    }

    /// <summary>워드 연속 쓰기(WRS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteWords(KeyenceHostLinkAddress address, ushort count, byte[] wordData)
    {
        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != count * 2)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        var sb = new StringBuilder();
        sb.Append("WRS ");
        sb.Append(address.WireString);
        sb.Append(' ');
        sb.Append(count);

        for (int i = 0; i < count; i++)
        {
            ushort value = (ushort)(wordData[i * 2] | (wordData[(i * 2) + 1] << 8));
            sb.Append(' ');
            sb.Append(value);
        }

        sb.Append('\r');
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>비트 연속 쓰기(WRS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteBits(KeyenceHostLinkAddress address, ushort count, byte[] bitData)
    {
        if (bitData is null)
        {
            throw new ArgumentNullException(nameof(bitData));
        }

        if (bitData.Length < count)
        {
            throw new ArgumentException("비트 데이터 개수가 부족합니다.", nameof(bitData));
        }

        var sb = new StringBuilder();
        sb.Append("WRS ");
        sb.Append(address.WireString);
        sb.Append(' ');
        sb.Append(count);

        for (int i = 0; i < count; i++)
        {
            sb.Append(' ');
            sb.Append(bitData[i] != 0 ? '1' : '0');
        }

        sb.Append('\r');
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
