using System;
using FieldLink.Plc.Conversion;

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
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "읽기 개수는 1 이상이어야 합니다.");
        }

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
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("쓸 워드 데이터가 비어 있습니다.", nameof(data));
        }

        ushort wordCount = address.Is32BitWord ? (ushort)(data.Length / 4) : (ushort)(data.Length / 2);
        ushort bodyLen = (ushort)(7 + data.Length);

        byte[] body = new byte[3 + bodyLen];
        FillBodyHeader(body, station, bodyLen, functionCode: 0x28, address, wordCount);
        Buffer.BlockCopy(data, 0, body, 10, data.Length);

        return WrapFrame(body);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (기능 코드 0x29)</summary>
    public static byte[] BuildWriteBitRequest(byte station, VigorAddress address, bool[] values)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (values is null || values.Length == 0)
        {
            throw new ArgumentException("쓸 비트 데이터가 비어 있습니다.", nameof(values));
        }

        int byteCount = (values.Length + 7) / 8;
        ushort bodyLen = (ushort)(7 + byteCount);
        byte[] body = new byte[3 + bodyLen];
        FillBodyHeader(body, station, bodyLen, functionCode: 0x29, address, (ushort)values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                body[10 + (i / 8)] |= (byte)(1 << (i % 8));
            }
        }

        return WrapFrame(body);
    }

    private static void FillBodyHeader(
        byte[] body,
        byte station,
        ushort bodyLen,
        byte functionCode,
        VigorAddress address,
        ushort count)
    {
        body[0] = station;
        BinaryBuffer.WriteUInt16LE(body, 1, bodyLen);
        body[3] = functionCode;
        body[4] = address.DeviceCode;
        body[5] = address.BcdAddress[0];
        body[6] = address.BcdAddress[1];
        body[7] = address.BcdAddress[2];
        BinaryBuffer.WriteUInt16LE(body, 8, count);
    }

    private static byte[] WrapFrame(byte[] body)
    {
        byte sum = 0;
        int dleCount = 0;
        for (int i = 0; i < body.Length; i++)
        {
            byte b = body[i];
            sum = (byte)(sum + b);
            if (b == Dle)
            {
                dleCount++;
            }
        }

        // DLE(1) + STX(1) + EscapedBody(body.Length + dleCount) + DLE(1) + ETX(1) + ChecksumHex(2)
        byte[] frame = new byte[6 + body.Length + dleCount];
        int offset = 0;
        frame[offset++] = Dle;
        frame[offset++] = Stx;

        for (int i = 0; i < body.Length; i++)
        {
            byte b = body[i];
            frame[offset++] = b;
            if (b == Dle)
            {
                frame[offset++] = Dle;
            }
        }

        frame[offset++] = Dle;
        frame[offset++] = Etx;
        BinaryBuffer.WriteAsciiHexByte(frame, offset, sum);

        return frame;
    }
}
