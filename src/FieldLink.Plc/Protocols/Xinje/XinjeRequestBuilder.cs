using System;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 요청 프레임 조립기입니다. (사양서 Part A §A.1)</summary>
public static class XinjeRequestBuilder
{
    /// <summary>확장 Modbus 연속 읽기 PDU를 생성합니다. (0x1E 비트, 0x20 워드)</summary>
    public static byte[] BuildExtendedReadPdu(XinjeAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "읽기 개수는 1 이상이어야 합니다.");
        }

        byte functionCode = address.IsBit ? (byte)0x1E : (byte)0x20;
        byte[] addrBytes = address.Get24BitAddressBytes();

        return
        [
            functionCode,
            address.ExtendedDeviceCode,
            addrBytes[0],
            addrBytes[1],
            addrBytes[2],
            (byte)((count >> 8) & 0xFF),
            (byte)(count & 0xFF),
        ];
    }

    /// <summary>확장 Modbus 연속 워드 쓰기 PDU를 생성합니다. (0x21)</summary>
    public static byte[] BuildExtendedWriteWordPdu(XinjeAddress address, ushort count, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count is < 1 or > 125)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "확장 워드 쓰기 수량은 1~125 사이여야 합니다.");
        }

        if (data is null || data.Length < count * 2)
        {
            throw new ArgumentException("워드 쓰기 데이터 길이가 부족합니다.", nameof(data));
        }

        byte[] addrBytes = address.Get24BitAddressBytes();
        byte byteCount = (byte)(count * 2);

        byte[] pdu = new byte[8 + byteCount];
        pdu[0] = 0x21;
        pdu[1] = address.ExtendedDeviceCode;
        pdu[2] = addrBytes[0];
        pdu[3] = addrBytes[1];
        pdu[4] = addrBytes[2];
        BinaryBuffer.WriteUInt16BE(pdu, 5, count);
        pdu[7] = byteCount;
        Buffer.BlockCopy(data, 0, pdu, 8, byteCount);

        return pdu;
    }

    /// <summary>확장 Modbus 연속 비트 쓰기 PDU를 생성합니다. (0x1F)</summary>
    public static byte[] BuildExtendedWriteBitPdu(XinjeAddress address, ushort count, bool[] bits)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count is < 1 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "확장 비트 쓰기 수량은 1~2000 사이여야 합니다.");
        }

        if (bits is null || bits.Length < count)
        {
            throw new ArgumentException("비트 쓰기 배열 길이가 부족합니다.", nameof(bits));
        }

        byte[] addrBytes = address.Get24BitAddressBytes();
        byte byteCount = (byte)((count + 7) / 8);

        byte[] pdu = new byte[8 + byteCount];
        pdu[0] = 0x1F;
        pdu[1] = address.ExtendedDeviceCode;
        pdu[2] = addrBytes[0];
        pdu[3] = addrBytes[1];
        pdu[4] = addrBytes[2];
        BinaryBuffer.WriteUInt16BE(pdu, 5, count);
        pdu[7] = byteCount;

        for (int i = 0; i < count; i++)
        {
            if (bits[i])
            {
                pdu[8 + (i / 8)] |= (byte)(1 << (i % 8));
            }
        }

        return pdu;
    }

    /// <summary>전송 인코딩에 따라 PDU를 ADU 프레임으로 래핑합니다.</summary>
    public static byte[] WrapAdu(byte[] pdu, byte station, XinjeTransportEncoding encoding, ushort transactionId)
    {
        return encoding == XinjeTransportEncoding.Tcp
            ? ModbusRequestBuilder.BuildTcpAdu(transactionId, station, pdu)
            : ModbusRequestBuilder.BuildRtuAdu(station, pdu);
    }
}
