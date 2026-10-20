using System;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 요청 프레임 조립기입니다. (사양서 Part A §A.1)</summary>
public static class XinjeRequestBuilder
{
    /// <summary>확장 Modbus 연속 읽기 PDU를 생성합니다. (0x1E 비트, 0x20 워드)</summary>
    public static byte[] BuildExtendedReadPdu(XinjeAddress address, ushort count)
    {
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
        byte[] addrBytes = address.Get24BitAddressBytes();
        byte byteCount = (byte)(count * 2);

        byte[] pdu = new byte[8 + byteCount];
        pdu[0] = 0x21;
        pdu[1] = address.ExtendedDeviceCode;
        pdu[2] = addrBytes[0];
        pdu[3] = addrBytes[1];
        pdu[4] = addrBytes[2];
        pdu[5] = (byte)((count >> 8) & 0xFF);
        pdu[6] = (byte)(count & 0xFF);
        pdu[7] = byteCount;
        Array.Copy(data, 0, pdu, 8, byteCount);

        return pdu;
    }

    /// <summary>확장 Modbus 연속 비트 쓰기 PDU를 생성합니다. (0x1F)</summary>
    public static byte[] BuildExtendedWriteBitPdu(XinjeAddress address, ushort count, bool[] bits)
    {
        byte[] addrBytes = address.Get24BitAddressBytes();
        byte byteCount = (byte)((count + 7) / 8);
        byte[] packed = new byte[byteCount];
        for (int i = 0; i < count; i++)
        {
            if (bits[i])
            {
                packed[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        byte[] pdu = new byte[8 + byteCount];
        pdu[0] = 0x1F;
        pdu[1] = address.ExtendedDeviceCode;
        pdu[2] = addrBytes[0];
        pdu[3] = addrBytes[1];
        pdu[4] = addrBytes[2];
        pdu[5] = (byte)((count >> 8) & 0xFF);
        pdu[6] = (byte)(count & 0xFF);
        pdu[7] = byteCount;
        Array.Copy(packed, 0, pdu, 8, byteCount);

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
