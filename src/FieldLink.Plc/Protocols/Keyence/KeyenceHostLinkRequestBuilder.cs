using System;
using System.Globalization;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) ASCII 요청 프레임을 생성하는 빌더입니다. (사양서 Part A §A.1)</summary>
public static class KeyenceHostLinkRequestBuilder
{
    private const int BytesPerWord = 2;
    private const int BytesPerDWord = 4;

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
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string command = $"RDS {address.WireString} {count}\r";
        return Encoding.ASCII.GetBytes(command);
    }

    /// <summary>워드 연속 쓰기(WRS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteWords(KeyenceHostLinkAddress address, ushort count, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != count * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("WRS ");
        commandBuilder.Append(address.WireString);
        commandBuilder.Append(' ');
        commandBuilder.Append(count);

        for (int i = 0; i < count; i++)
        {
            int byteOffset = i * BytesPerWord;
            ushort wordValue = (ushort)(wordData[byteOffset] | (wordData[byteOffset + 1] << 8));
            commandBuilder.Append(' ');
            commandBuilder.Append(wordValue);
        }

        commandBuilder.Append('\r');
        return Encoding.ASCII.GetBytes(commandBuilder.ToString());
    }

    /// <summary>32비트 디바이스(Z, AT, TC, TS, CC, CS) 연속 쓰기(WRS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteDWords(KeyenceHostLinkAddress address, ushort count, byte[] dwordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (dwordData is null)
        {
            throw new ArgumentNullException(nameof(dwordData));
        }

        if (dwordData.Length != count * BytesPerDWord)
        {
            throw new ArgumentException("데이터 길이가 32비트 디바이스 개수와 일치하지 않습니다.", nameof(dwordData));
        }

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("WRS ");
        commandBuilder.Append(address.WireString);
        commandBuilder.Append(' ');
        commandBuilder.Append(count);

        for (int i = 0; i < count; i++)
        {
            int byteOffset = i * BytesPerDWord;
            uint dwordValue = BinaryBuffer.ReadUInt32LittleEndian(dwordData, byteOffset);
            commandBuilder.Append(' ');
            commandBuilder.Append(dwordValue.ToString(CultureInfo.InvariantCulture));
        }

        commandBuilder.Append('\r');
        return Encoding.ASCII.GetBytes(commandBuilder.ToString());
    }

    /// <summary>비트 연속 쓰기(WRS) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteBits(KeyenceHostLinkAddress address, ushort count, byte[] bitData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (bitData is null)
        {
            throw new ArgumentNullException(nameof(bitData));
        }

        if (bitData.Length < count)
        {
            throw new ArgumentException("비트 데이터 개수가 부족합니다.", nameof(bitData));
        }

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("WRS ");
        commandBuilder.Append(address.WireString);
        commandBuilder.Append(' ');
        commandBuilder.Append(count);

        for (int i = 0; i < count; i++)
        {
            commandBuilder.Append(' ');
            commandBuilder.Append(bitData[i] != 0 ? '1' : '0');
        }

        commandBuilder.Append('\r');
        return Encoding.ASCII.GetBytes(commandBuilder.ToString());
    }
}
