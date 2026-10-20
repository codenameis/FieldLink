using System;
using System.IO;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class VigorRequestBuilder
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (기능 코드 0x20/0x21)</summary>
    public static byte[] BuildReadRequest(byte station, VigorAddress address, ushort count)
    {
        byte functionCode = address.IsBit ? (byte)0x21 : (byte)0x20;
        byte[] body =
        [
            station,
            0x07,
            0x00,
            functionCode,
            address.DeviceCode,
            address.BcdAddress[0],
            address.BcdAddress[1],
            address.BcdAddress[2],
            (byte)(count & 0xFF),
            (byte)((count >> 8) & 0xFF),
        ];

        return WrapFrame(body);
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (기능 코드 0x28)</summary>
    public static byte[] BuildWriteWordRequest(byte station, VigorAddress address, byte[] data)
    {
        ushort wordCount = address.Is32BitWord ? (ushort)(data.Length / 4) : (ushort)(data.Length / 2);
        ushort bodyLen = (ushort)(7 + data.Length);

        byte[] body = new byte[3 + bodyLen];
        body[0] = station;
        body[1] = (byte)(bodyLen & 0xFF);
        body[2] = (byte)((bodyLen >> 8) & 0xFF);
        body[3] = 0x28;
        body[4] = address.DeviceCode;
        body[5] = address.BcdAddress[0];
        body[6] = address.BcdAddress[1];
        body[7] = address.BcdAddress[2];
        body[8] = (byte)(wordCount & 0xFF);
        body[9] = (byte)((wordCount >> 8) & 0xFF);
        Array.Copy(data, 0, body, 10, data.Length);

        return WrapFrame(body);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (기능 코드 0x29)</summary>
    public static byte[] BuildWriteBitRequest(byte station, VigorAddress address, bool[] values)
    {
        int byteCount = (values.Length + 7) / 8;
        byte[] packed = new byte[byteCount];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                packed[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        ushort bodyLen = (ushort)(7 + byteCount);
        byte[] body = new byte[3 + bodyLen];
        body[0] = station;
        body[1] = (byte)(bodyLen & 0xFF);
        body[2] = (byte)((bodyLen >> 8) & 0xFF);
        body[3] = 0x29;
        body[4] = address.DeviceCode;
        body[5] = address.BcdAddress[0];
        body[6] = address.BcdAddress[1];
        body[7] = address.BcdAddress[2];
        body[8] = (byte)(values.Length & 0xFF);
        body[9] = (byte)((values.Length >> 8) & 0xFF);
        Array.Copy(packed, 0, body, 10, byteCount);

        return WrapFrame(body);
    }

    private static byte[] WrapFrame(byte[] body)
    {
        byte sum = 0;
        foreach (byte b in body)
        {
            sum = (byte)(sum + b);
        }

        string hex = sum.ToString("X2");
        byte c0 = (byte)hex[0];
        byte c1 = (byte)hex[1];

        using (MemoryStream ms = new MemoryStream())
        {
            ms.WriteByte(Dle);
            ms.WriteByte(Stx);
            foreach (byte b in body)
            {
                if (b == Dle)
                {
                    ms.WriteByte(Dle);
                    ms.WriteByte(Dle);
                }
                else
                {
                    ms.WriteByte(b);
                }
            }

            ms.WriteByte(Dle);
            ms.WriteByte(Etx);
            ms.WriteByte(c0);
            ms.WriteByte(c1);

            return ms.ToArray();
        }
    }
}
