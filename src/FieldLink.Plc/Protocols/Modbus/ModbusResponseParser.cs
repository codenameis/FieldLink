using System;
using System.Text;
using FieldLink.Plc.Checksums;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus 응답 프레임 및 PDU 바이트를 검증하고 데이터를 추출하는 정적 순수 함수 모음입니다. (사양서 §3-2, §3-3, §3-4)</summary>
public static class ModbusResponseParser
{
    private const byte AsciiColon = 0x3A; // ':'
    private const byte AsciiCarriageReturn = 0x0D; // '\r'
    private const byte AsciiLineFeed = 0x0A; // '\n'

    /// <summary>TCP ADU 프레임의 헤더(MBAP)를 검증하고 PDU 바이트 배열을 추출합니다.</summary>
    public static byte[] UnwrapTcpAdu(byte[] adu, ushort expectedTransactionId, byte expectedUnitId)
    {
        if (adu is null)
        {
            throw new ArgumentNullException(nameof(adu));
        }

        if (adu.Length < 7)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"TCP ADU 길이가 너무 짧습니다. (길이: {adu.Length})");
        }

        ushort transactionId = (ushort)((adu[0] << 8) | adu[1]);
        if (transactionId != expectedTransactionId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"트랜잭션 ID가 일치하지 않습니다. (기대값: 0x{expectedTransactionId:X4}, 수신값: 0x{transactionId:X4})");
        }

        ushort protocolId = (ushort)((adu[2] << 8) | adu[3]);
        if (protocolId != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Modbus TCP 프로토콜 ID가 0이 아닙니다. (수신값: 0x{protocolId:X4})");
        }

        int lengthField = (adu[4] << 8) | adu[5];
        if (adu.Length != 6 + lengthField)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"MBAP 헤더의 길이 필드({lengthField})와 실제 프레임 길이({adu.Length})가 일치하지 않습니다.");
        }

        byte unitId = adu[6];
        if (unitId != expectedUnitId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"유닛 ID가 일치하지 않습니다. (기대값: {expectedUnitId}, 수신값: {unitId})");
        }

        byte[] pdu = new byte[adu.Length - 7];
        Array.Copy(adu, 7, pdu, 0, pdu.Length);
        return pdu;
    }

    /// <summary>RTU ADU 프레임의 국번과 CRC-16을 검증하고 PDU 바이트 배열을 추출합니다.</summary>
    public static byte[] UnwrapRtuAdu(byte[] adu, byte expectedUnitId)
    {
        if (adu is null)
        {
            throw new ArgumentNullException(nameof(adu));
        }

        if (adu.Length < 4) // 국번 1 + 최소 PDU 1 + CRC 2
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"RTU ADU 길이가 너무 짧습니다. (길이: {adu.Length})");
        }

        byte[] calculatedCrc = Crc16Modbus.CalculateBytes(adu, 0, adu.Length - 2);
        if (adu[adu.Length - 2] != calculatedCrc[0] || adu[adu.Length - 1] != calculatedCrc[1])
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Modbus RTU 응답의 CRC-16 체크섬이 일치하지 않습니다.");
        }

        byte unitId = adu[0];
        if (unitId != expectedUnitId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"국번이 일치하지 않습니다. (기대값: {expectedUnitId}, 수신값: {unitId})");
        }

        byte[] pdu = new byte[adu.Length - 3];
        Array.Copy(adu, 1, pdu, 0, pdu.Length);
        return pdu;
    }

    /// <summary>ASCII ADU 프레임의 구분자, 국번, LRC를 검증하고 PDU 바이트 배열을 추출합니다.</summary>
    public static byte[] UnwrapAsciiAdu(byte[] adu, byte expectedUnitId)
    {
        if (adu is null)
        {
            throw new ArgumentNullException(nameof(adu));
        }

        if (adu.Length < 9) // ':' + 국번 2자 + FC 2자 + LRC 2자 + CRLF 2자 = 9자 이상
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"ASCII ADU 길이가 너무 짧습니다. (길이: {adu.Length})");
        }

        if (adu[0] != AsciiColon || adu[adu.Length - 2] != AsciiCarriageReturn || adu[adu.Length - 1] != AsciiLineFeed)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Modbus ASCII 프레임의 시작 또는 종료 구분자가 올바르지 않습니다.");
        }

        string hexString = Encoding.ASCII.GetString(adu, 1, adu.Length - 3);
        byte[] rawBytes;
        try
        {
            rawBytes = AsciiHex.GetBytes(hexString);
        }
        catch (FormatException exception)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Modbus ASCII 프레임에 16진수가 아닌 문자가 포함되어 있습니다.", exception);
        }

        byte calculatedLrc = LongitudinalRedundancyCheck.Calculate(rawBytes, 0, rawBytes.Length - 1);
        if (rawBytes[rawBytes.Length - 1] != calculatedLrc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Modbus ASCII 응답의 LRC 체크섬이 일치하지 않습니다.");
        }

        byte unitId = rawBytes[0];
        if (unitId != expectedUnitId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"국번이 일치하지 않습니다. (기대값: {expectedUnitId}, 수신값: {unitId})");
        }

        byte[] pdu = new byte[rawBytes.Length - 2];
        Array.Copy(rawBytes, 1, pdu, 0, pdu.Length);
        return pdu;
    }

    /// <summary>레지스터 읽기 응답 PDU(FC 0x03, 0x04)를 검증하고 연속된 레지스터 바이트를 반환합니다.</summary>
    public static byte[] ParseReadRegisters(byte[] pdu, ModbusFunctionCode expectedFunctionCode, ushort expectedRegisterCount)
    {
        ValidatePduHeader(pdu, expectedFunctionCode);

        int expectedByteCount = expectedRegisterCount * 2;
        byte byteCount = pdu[1];

        if (byteCount != expectedByteCount || pdu.Length != 2 + expectedByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"레지스터 읽기 응답의 바이트 수({byteCount})가 기대 수({expectedByteCount})와 일치하지 않습니다.");
        }

        byte[] registerBytes = new byte[expectedByteCount];
        Array.Copy(pdu, 2, registerBytes, 0, expectedByteCount);
        return registerBytes;
    }

    /// <summary>코일/이산입력 읽기 응답 PDU(FC 0x01, 0x02)를 검증하고 비트당 1바이트 형식의 불리언 바이트 배열을 반환합니다.</summary>
    public static byte[] ParseReadBits(byte[] pdu, ModbusFunctionCode expectedFunctionCode, ushort expectedBitCount)
    {
        ValidatePduHeader(pdu, expectedFunctionCode);

        int expectedByteCount = BitPacking.GetPackedByteCount(expectedBitCount);
        byte byteCount = pdu[1];

        if (byteCount != expectedByteCount || pdu.Length != 2 + expectedByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"비트 읽기 응답의 바이트 수({byteCount})가 기대 수({expectedByteCount})와 일치하지 않습니다.");
        }

        bool[] bits = BitPacking.Unpack(pdu, offset: 2, bitCount: expectedBitCount);
        byte[] result = new byte[expectedBitCount];
        for (int i = 0; i < expectedBitCount; i++)
        {
            result[i] = (byte)(bits[i] ? 1 : 0);
        }

        return result;
    }

    /// <summary>단일 쓰기 응답 PDU(FC 0x05, 0x06)가 요청 PDU와 동일한 에코인지 검증합니다.</summary>
    public static void ParseWriteSingleResponse(byte[] responsePdu, byte[] requestPdu)
    {
        ValidatePduHeader(responsePdu, (ModbusFunctionCode)requestPdu[0]);

        if (responsePdu.Length != 5 || requestPdu.Length != 5)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "단일 쓰기 응답 길이가 5바이트가 아닙니다.");
        }

        for (int i = 0; i < 5; i++)
        {
            if (responsePdu[i] != requestPdu[i])
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "단일 쓰기 에코 응답이 요청 바이트와 일치하지 않습니다.");
            }
        }
    }

    /// <summary>다중 쓰기 응답 PDU(FC 0x0F, 0x10)의 시작 주소와 수량이 요청과 일치하는지 검증합니다.</summary>
    public static void ParseWriteMultipleResponse(byte[] responsePdu, ModbusFunctionCode expectedFunctionCode, ushort expectedStartAddress, ushort expectedCount)
    {
        ValidatePduHeader(responsePdu, expectedFunctionCode);

        if (responsePdu.Length != 5)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "다중 쓰기 응답 길이가 5바이트가 아닙니다.");
        }

        ushort startAddress = (ushort)((responsePdu[1] << 8) | responsePdu[2]);
        ushort count = (ushort)((responsePdu[3] << 8) | responsePdu[4]);

        if (startAddress != expectedStartAddress || count != expectedCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"다중 쓰기 확인 응답의 주소(0x{startAddress:X4}) 또는 수량({count})이 요청과 일치하지 않습니다.");
        }
    }

    private static void ValidatePduHeader(byte[] pdu, ModbusFunctionCode expectedFunctionCode)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        if (pdu.Length < 2)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Modbus 응답 PDU 길이가 너무 짧습니다. (길이: {pdu.Length})");
        }

        byte responseFc = pdu[0];
        byte expectedFc = (byte)expectedFunctionCode;

        if (responseFc == (expectedFc | 0x80))
        {
            if (pdu.Length != 2)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, "Modbus 예외 응답 길이가 2바이트가 아닙니다.");
            }

            var exceptionCode = (ModbusExceptionCode)pdu[1];
            throw new PlcDeviceException((int)exceptionCode, exceptionCode.GetDescription());
        }

        if (responseFc != expectedFc)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"기대한 기능 코드(0x{expectedFc:X2})와 다른 기능 코드(0x{responseFc:X2})가 수신되었습니다.");
        }
    }
}
