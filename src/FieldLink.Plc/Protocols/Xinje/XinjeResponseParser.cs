using System;
using FieldLink.Plc.Checksums;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 응답 프레임 파서입니다. (사양서 Part A §A.2)</summary>
public static class XinjeResponseParser
{
    /// <summary>ADU 응답에서 순수 PDU 바이트를 추출하고 무결성을 검증합니다.</summary>
    public static byte[] ExtractPdu(byte[] adu, byte expectedStation, XinjeTransportEncoding encoding, ushort expectedTransactionId)
    {
        if (adu is null)
        {
            throw new ArgumentNullException(nameof(adu));
        }

        if (encoding == XinjeTransportEncoding.Tcp)
        {
            if (adu.Length < 7)
            {
                throw new InvalidOperationException($"Modbus TCP 응답이 너무 짧습니다: {adu.Length}바이트");
            }

            ushort transactionId = (ushort)((adu[0] << 8) | adu[1]);
            if (transactionId != expectedTransactionId)
            {
                throw new InvalidOperationException($"트랜잭션 ID 불일치: 기대={expectedTransactionId}, 수신={transactionId}");
            }

            byte unitId = adu[6];
            if (unitId != expectedStation)
            {
                throw new InvalidOperationException($"유닛 ID 불일치: 기대={expectedStation}, 수신={unitId}");
            }

            int pduLen = adu.Length - 7;
            byte[] pdu = new byte[pduLen];
            Array.Copy(adu, 7, pdu, 0, pduLen);
            return pdu;
        }
        else
        {
            if (adu.Length < 5)
            {
                throw new InvalidOperationException($"Modbus RTU 응답이 너무 짧습니다: {adu.Length}바이트");
            }

            byte station = adu[0];
            if (station != expectedStation)
            {
                throw new InvalidOperationException($"국번 불일치: 기대={expectedStation}, 수신={station}");
            }

            ushort expectedCrc = Crc16Modbus.Calculate(adu, 0, adu.Length - 2);
            byte crcLow = (byte)(expectedCrc & 0xFF);
            byte crcHigh = (byte)((expectedCrc >> 8) & 0xFF);

            if (adu[adu.Length - 2] != crcLow || adu[adu.Length - 1] != crcHigh)
            {
                throw new InvalidOperationException("Modbus RTU CRC-16 체크섬 검증에 실패했습니다.");
            }

            int pduLen = adu.Length - 3;
            byte[] pdu = new byte[pduLen];
            Array.Copy(adu, 1, pdu, 0, pduLen);
            return pdu;
        }
    }

    /// <summary>확장 Modbus 읽기 응답 PDU(0x1E, 0x20)에서 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseExtendedReadResponse(byte[] pdu, byte expectedFc)
    {
        if (pdu.Length < 2)
        {
            throw new InvalidOperationException("PDU 길이가 너무 짧습니다.");
        }

        byte fc = pdu[0];
        if ((fc & 0x80) != 0)
        {
            byte err = pdu.Length > 1 ? pdu[1] : (byte)0;
            throw new InvalidOperationException($"XINJE Modbus 예외 응답 수신: 기능코드=0x{fc:X2}, 오류코드=0x{err:X2}");
        }

        if (fc != expectedFc)
        {
            throw new InvalidOperationException($"예상하지 않은 기능 코드 응답: 기대=0x{expectedFc:X2}, 수신=0x{fc:X2}");
        }

        int byteCount = pdu[1];
        if (pdu.Length < 2 + byteCount)
        {
            throw new InvalidOperationException($"응답 데이터 바이트가 선언된 바이트 수({byteCount})보다 적습니다.");
        }

        byte[] data = new byte[byteCount];
        Array.Copy(pdu, 2, data, 0, byteCount);
        return data;
    }

    /// <summary>확장 Modbus 쓰기 응답 PDU(0x1F, 0x21)를 검증합니다.</summary>
    public static void VerifyExtendedWriteResponse(byte[] pdu, byte expectedFc)
    {
        if (pdu.Length < 1)
        {
            throw new InvalidOperationException("PDU 길이가 너무 짧습니다.");
        }

        byte fc = pdu[0];
        if ((fc & 0x80) != 0)
        {
            byte err = pdu.Length > 1 ? pdu[1] : (byte)0;
            throw new InvalidOperationException($"XINJE Modbus 예외 응답 수신: 기능코드=0x{fc:X2}, 오류코드=0x{err:X2}");
        }

        if (fc != expectedFc)
        {
            throw new InvalidOperationException($"예상하지 않은 기능 코드 응답: 기대=0x{expectedFc:X2}, 수신=0x{fc:X2}");
        }
    }
}
