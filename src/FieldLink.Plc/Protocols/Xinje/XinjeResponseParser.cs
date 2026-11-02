using System;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 응답 프레임 파서입니다. (사양서 Part A §A.2)</summary>
public static class XinjeResponseParser
{
    private const byte ExceptionResponseFlag = 0x80;
    private const int FunctionCodeOffset = 0;
    private const int ExceptionCodeOffset = 1;

    private const int ReadByteCountOffset = 1;
    private const int ReadDataOffset = 2;
    private const int ReadResponseHeaderLength = 2;

    private const int ExtendedWriteResponsePduLength = 7;
    private const int DeviceCodeOffset = 1;
    private const int AddressHighOffset = 2;
    private const int AddressMidOffset = 3;
    private const int AddressLowOffset = 4;
    private const int CountHighOffset = 5;
    private const int Address24BitMask = 0xFFFFFF;

    /// <summary>ADU 응답에서 순수 PDU 바이트를 추출하고 무결성을 검증합니다.</summary>
    /// <param name="adu">수신된 ADU 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 슬레이브 국번입니다.</param>
    /// <param name="encoding">RTU 또는 TCP 전송 인코딩입니다.</param>
    /// <param name="expectedTransactionId">TCP MBAP 기대 트랜잭션 ID입니다.</param>
    /// <returns>추출된 순수 PDU 바이트 배열입니다.</returns>
    public static byte[] ExtractPdu(
        byte[] adu,
        byte expectedStation,
        XinjeTransportEncoding encoding,
        ushort expectedTransactionId)
    {
        return encoding == XinjeTransportEncoding.Tcp
            ? ModbusResponseParser.UnwrapTcpAdu(adu, expectedTransactionId, expectedStation)
            : ModbusResponseParser.UnwrapRtuAdu(adu, expectedStation);
    }

    /// <summary>확장 Modbus 읽기 응답 PDU(0x1E, 0x20)에서 데이터 바이트를 추출합니다.</summary>
    /// <param name="pdu">추출된 응답 PDU 바이트 배열입니다.</param>
    /// <param name="expectedFunctionCode">기대하는 확장 읽기 기능 코드(0x1E 또는 0x20)입니다.</param>
    /// <returns>추출된 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseExtendedReadResponse(byte[] pdu, byte expectedFunctionCode)
    {
        ValidateExtendedPduHeader(pdu, expectedFunctionCode, minimumLength: ReadResponseHeaderLength);

        int declaredByteCount = pdu[ReadByteCountOffset];
        if (pdu.Length != ReadResponseHeaderLength + declaredByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 데이터 바이트 길이({pdu.Length - ReadResponseHeaderLength})가 선언된 바이트 수({declaredByteCount})와 일치하지 않습니다.");
        }

        byte[] data = new byte[declaredByteCount];
        Buffer.BlockCopy(pdu, ReadDataOffset, data, 0, declaredByteCount);
        return data;
    }

    /// <summary>확장 Modbus 읽기 응답 PDU(0x1E, 0x20)에서 기대 바이트 수를 검증하고 데이터를 추출합니다.</summary>
    /// <param name="pdu">추출된 응답 PDU 바이트 배열입니다.</param>
    /// <param name="expectedFunctionCode">기대하는 확장 읽기 기능 코드(0x1E 또는 0x20)입니다.</param>
    /// <param name="expectedByteCount">요청 수량에 따른 기대 데이터 바이트 수입니다.</param>
    /// <returns>추출된 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseExtendedReadResponse(byte[] pdu, byte expectedFunctionCode, int expectedByteCount)
    {
        ValidateExtendedPduHeader(pdu, expectedFunctionCode, minimumLength: ReadResponseHeaderLength);

        int declaredByteCount = pdu[ReadByteCountOffset];
        if (declaredByteCount != expectedByteCount || pdu.Length != ReadResponseHeaderLength + expectedByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"확장 읽기 응답의 바이트 수(선언={declaredByteCount}, 실제={pdu.Length - ReadResponseHeaderLength})가 기대 바이트 수({expectedByteCount})와 일치하지 않습니다.");
        }

        byte[] data = new byte[expectedByteCount];
        Buffer.BlockCopy(pdu, ReadDataOffset, data, 0, expectedByteCount);
        return data;
    }

    /// <summary>확장 Modbus 쓰기 응답 PDU(0x1F, 0x21)의 헤더와 길이(7바이트)를 검증합니다.</summary>
    /// <param name="pdu">추출된 응답 PDU 바이트 배열입니다.</param>
    /// <param name="expectedFunctionCode">기대하는 확장 쓰기 기능 코드(0x1F 또는 0x21)입니다.</param>
    public static void VerifyExtendedWriteResponse(byte[] pdu, byte expectedFunctionCode)
    {
        ValidateExtendedPduHeader(pdu, expectedFunctionCode, minimumLength: ExtendedWriteResponsePduLength);

        if (pdu.Length != ExtendedWriteResponsePduLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"확장 쓰기 응답 PDU 길이({pdu.Length})가 기대 길이({ExtendedWriteResponsePduLength})와 일치하지 않습니다.");
        }
    }

    /// <summary>확장 Modbus 쓰기 응답 PDU(0x1F, 0x21)의 길이(7바이트)와 디바이스 코드, 24비트 주소, 수량 에코를 검증합니다.</summary>
    /// <param name="pdu">추출된 응답 PDU 바이트 배열입니다.</param>
    /// <param name="expectedFunctionCode">기대하는 확장 쓰기 기능 코드(0x1F 또는 0x21)입니다.</param>
    /// <param name="expectedAddress">요청 시 전송한 시작 주소입니다.</param>
    /// <param name="expectedCount">요청 시 전송한 비트 또는 워드 수량입니다.</param>
    public static void VerifyExtendedWriteResponse(
        byte[] pdu,
        byte expectedFunctionCode,
        XinjeAddress expectedAddress,
        ushort expectedCount)
    {
        if (expectedAddress is null)
        {
            throw new ArgumentNullException(nameof(expectedAddress));
        }

        VerifyExtendedWriteResponse(pdu, expectedFunctionCode);
        ValidateExtendedWriteEcho(pdu, expectedAddress, expectedCount);
    }

    private static void ValidateExtendedWriteEcho(byte[] pdu, XinjeAddress expectedAddress, ushort expectedCount)
    {
        byte responseDeviceCode = pdu[DeviceCodeOffset];
        if (responseDeviceCode != expectedAddress.ExtendedDeviceCode)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"확장 쓰기 응답의 디바이스 코드(0x{responseDeviceCode:X2})가 요청(0x{expectedAddress.ExtendedDeviceCode:X2})과 일치하지 않습니다.");
        }

        int highByte = pdu[AddressHighOffset] << 16;
        int middleByte = pdu[AddressMidOffset] << 8;
        int lowByte = pdu[AddressLowOffset];
        int responseAddress = highByte | middleByte | lowByte;
        int expectedAddressNumber = expectedAddress.Number & Address24BitMask;

        if (responseAddress != expectedAddressNumber)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"확장 쓰기 응답의 시작 주소(0x{responseAddress:X6})가 요청(0x{expectedAddressNumber:X6})과 일치하지 않습니다.");
        }

        ushort responseCount = BinaryBuffer.ReadUInt16BigEndian(pdu, CountHighOffset);
        if (responseCount != expectedCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"확장 쓰기 응답의 수량({responseCount})이 요청({expectedCount})과 일치하지 않습니다.");
        }
    }

    private static void ValidateExtendedPduHeader(byte[] pdu, byte expectedFunctionCode, int minimumLength)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        if (pdu.Length < 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "PDU 길이가 너무 짧습니다.");
        }

        byte functionCode = pdu[FunctionCodeOffset];
        if ((functionCode & ExceptionResponseFlag) != 0)
        {
            byte errorCode = pdu.Length > ExceptionCodeOffset ? pdu[ExceptionCodeOffset] : (byte)0;
            throw new PlcDeviceException(errorCode, $"XINJE Modbus 예외 응답 수신: 기능코드=0x{functionCode:X2}, 오류코드=0x{errorCode:X2}");
        }

        if (functionCode != expectedFunctionCode)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"예상하지 않은 기능 코드 응답: 기대=0x{expectedFunctionCode:X2}, 수신=0x{functionCode:X2}");
        }

        if (pdu.Length < minimumLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"PDU 길이가 너무 짧습니다: {pdu.Length}바이트");
        }
    }
}
