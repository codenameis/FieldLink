using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class YokogawaRequestBuilder
{
    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (명령 0x11 / 0x01)</summary>
    public static byte[] BuildReadRequest(byte cpuNumber, YokogawaAddress address, ushort count)
    {
        byte command = address.IsBit ? (byte)0x01 : (byte)0x11;
        byte[] frame = new byte[12];
        frame[0] = command;
        frame[1] = cpuNumber;
        frame[2] = 0x00;
        frame[3] = 0x08; // Parameter size = 8
        frame[4] = (byte)((address.Attribute >> 8) & 0xFF);
        frame[5] = (byte)(address.Attribute & 0xFF);
        frame[6] = (byte)((address.Number >> 24) & 0xFF);
        frame[7] = (byte)((address.Number >> 16) & 0xFF);
        frame[8] = (byte)((address.Number >> 8) & 0xFF);
        frame[9] = (byte)(address.Number & 0xFF);
        frame[10] = (byte)((count >> 8) & 0xFF);
        frame[11] = (byte)(count & 0xFF);

        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (명령 0x12)</summary>
    public static byte[] BuildWriteWordRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] wordBytes)
    {
        ushort paramSize = (ushort)(8 + wordBytes.Length);
        byte[] frame = new byte[4 + paramSize];
        frame[0] = 0x12;
        frame[1] = cpuNumber;
        frame[2] = (byte)((paramSize >> 8) & 0xFF);
        frame[3] = (byte)(paramSize & 0xFF);
        frame[4] = (byte)((address.Attribute >> 8) & 0xFF);
        frame[5] = (byte)(address.Attribute & 0xFF);
        frame[6] = (byte)((address.Number >> 24) & 0xFF);
        frame[7] = (byte)((address.Number >> 16) & 0xFF);
        frame[8] = (byte)((address.Number >> 8) & 0xFF);
        frame[9] = (byte)(address.Number & 0xFF);
        frame[10] = (byte)((count >> 8) & 0xFF);
        frame[11] = (byte)(count & 0xFF);
        Array.Copy(wordBytes, 0, frame, 12, wordBytes.Length);

        return frame;
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (명령 0x02)</summary>
    public static byte[] BuildWriteBitRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] bitBytes)
    {
        ushort paramSize = (ushort)(8 + bitBytes.Length);
        byte[] frame = new byte[4 + paramSize];
        frame[0] = 0x02;
        frame[1] = cpuNumber;
        frame[2] = (byte)((paramSize >> 8) & 0xFF);
        frame[3] = (byte)(paramSize & 0xFF);
        frame[4] = (byte)((address.Attribute >> 8) & 0xFF);
        frame[5] = (byte)(address.Attribute & 0xFF);
        frame[6] = (byte)((address.Number >> 24) & 0xFF);
        frame[7] = (byte)((address.Number >> 16) & 0xFF);
        frame[8] = (byte)((address.Number >> 8) & 0xFF);
        frame[9] = (byte)(address.Number & 0xFF);
        frame[10] = (byte)((count >> 8) & 0xFF);
        frame[11] = (byte)(count & 0xFF);
        Array.Copy(bitBytes, 0, frame, 12, bitBytes.Length);

        return frame;
    }
}
