using System;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 응답 프레임 파서입니다. (사양서 Part A §A.2)</summary>
public static class XinjeResponseParser
{
    /// <summary>ADU 응답에서 순수 PDU 바이트를 추출하고 무결성을 검증합니다.</summary>
    public static byte[] ExtractPdu(byte[] adu, byte expectedStation, XinjeTransportEncoding encoding, ushort expectedTransactionId)
    {
        return encoding == XinjeTransportEncoding.Tcp
            ? ModbusResponseParser.UnwrapTcpAdu(adu, expectedTransactionId, expectedStation)
            : ModbusResponseParser.UnwrapRtuAdu(adu, expectedStation);
    }

    /// <summary>확장 Modbus 읽기 응답 PDU(0x1E, 0x20)에서 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseExtendedReadResponse(byte[] pdu, byte expectedFc)
    {
        ValidateExtendedPduHeader(pdu, expectedFc, minimumLength: 2);

        int byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 데이터 바이트가 선언된 바이트 수({byteCount})보다 적습니다.");
        }

        byte[] data = new byte[byteCount];
        Buffer.BlockCopy(pdu, 2, data, 0, byteCount);
        return data;
    }

    /// <summary>확장 Modbus 쓰기 응답 PDU(0x1F, 0x21)를 검증합니다.</summary>
    public static void VerifyExtendedWriteResponse(byte[] pdu, byte expectedFc)
    {
        ValidateExtendedPduHeader(pdu, expectedFc, minimumLength: 1);
    }

    private static void ValidateExtendedPduHeader(byte[] pdu, byte expectedFc, int minimumLength)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        if (pdu.Length < 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "PDU 길이가 너무 짧습니다.");
        }

        byte fc = pdu[0];
        if ((fc & 0x80) != 0)
        {
            byte err = pdu.Length > 1 ? pdu[1] : (byte)0;
            throw new PlcDeviceException(err, $"XINJE Modbus 예외 응답 수신: 기능코드=0x{fc:X2}, 오류코드=0x{err:X2}");
        }

        if (fc != expectedFc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"예상하지 않은 기능 코드 응답: 기대=0x{expectedFc:X2}, 수신=0x{fc:X2}");
        }

        if (pdu.Length < minimumLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"PDU 길이가 너무 짧습니다: {pdu.Length}바이트");
        }
    }
}
