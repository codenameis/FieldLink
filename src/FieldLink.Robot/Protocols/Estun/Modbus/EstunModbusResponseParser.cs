using System;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun Modbus TCP 응답 프레임을 검증하고 레지스터 페이로드를 추출하는 순수 정적 파서입니다.</summary>
public static class EstunModbusResponseParser
{
    private const int MbapPrefixLength = 6;
    private const int MinimumHeaderWithFunctionCode = 8;
    private const int ReadResponseByteCountOffset = 8;
    private const int ReadResponseDataOffset = 9;
    private const int WriteResponseFrameLength = 12;
    private const int WriteResponseAddressOffset = 8;
    private const int WriteResponseValueOrQuantityOffset = 10;
    private const int BytesPerRegister = 2;
    private const byte ExceptionBitMask = 0x80;
    private const byte ReadHoldingRegistersFunctionCode = 0x03;
    private const byte WriteSingleRegisterFunctionCode = 0x06;
    private const byte WriteMultipleRegistersFunctionCode = 0x10;
    private const byte ExceptionIllegalFunction = 0x01;
    private const byte ExceptionIllegalDataAddress = 0x02;
    private const byte ExceptionIllegalDataValue = 0x03;
    private const byte ExceptionSlaveDeviceFailure = 0x04;
    private const byte ExceptionSlaveDeviceBusy = 0x06;

    /// <summary>Holding Register 읽기(<c>FC 0x03</c>) 응답을 검증하고 레지스터 바이트 배열을 반환합니다.</summary>
    public static byte[] ParseReadHoldingRegisters(
        byte[] frame,
        ushort expectedTransactionId,
        byte expectedStation,
        ushort expectedQuantity)
    {
        ValidateHeaderAndException(frame, expectedTransactionId, expectedStation, ReadHoldingRegistersFunctionCode);

        int expectedByteCount = expectedQuantity * BytesPerRegister;
        if (frame.Length < ReadResponseDataOffset + expectedByteCount)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 읽기 응답 길이({frame.Length} 바이트)가 기대 레지스터 크기({expectedByteCount} 바이트)보다 짧습니다.",
                frame);
        }

        byte reportedByteCount = frame[ReadResponseByteCountOffset];
        if (reportedByteCount != expectedByteCount)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 응답 바이트 수({reportedByteCount})가 요청 크기({expectedByteCount})와 일치하지 않습니다.",
                frame);
        }

        byte[] payload = new byte[expectedByteCount];
        Array.Copy(frame, ReadResponseDataOffset, payload, 0, expectedByteCount);
        return payload;
    }

    /// <summary>단일 레지스터 쓰기(<c>FC 0x06</c>) 응답의 길이와 에코 주소·값을 검증합니다.</summary>
    public static void ValidateWriteSingleRegister(
        byte[] frame,
        ushort expectedTransactionId,
        byte expectedStation,
        ushort? expectedAddress = null,
        short? expectedValue = null)
    {
        ValidateHeaderAndException(frame, expectedTransactionId, expectedStation, WriteSingleRegisterFunctionCode);
        if (frame.Length != WriteResponseFrameLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 단일 쓰기 응답 길이가 규격({WriteResponseFrameLength} 바이트)과 일치하지 않습니다. (실제: {frame.Length} 바이트)",
                frame);
        }

        ushort echoedAddress = RobotBinaryBuffer.ReadUInt16BigEndian(frame, WriteResponseAddressOffset);
        if (expectedAddress.HasValue && echoedAddress != expectedAddress.Value)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 단일 쓰기 응답 주소({echoedAddress})가 요청 주소({expectedAddress.Value})와 일치하지 않습니다.",
                frame);
        }

        short echoedValue = RobotBinaryBuffer.ReadInt16BigEndian(frame, WriteResponseValueOrQuantityOffset);
        if (expectedValue.HasValue && echoedValue != expectedValue.Value)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 단일 쓰기 응답 값({echoedValue})이 요청 값({expectedValue.Value})과 일치하지 않습니다.",
                frame);
        }
    }

    /// <summary>다중 레지스터 쓰기(<c>FC 0x10</c>) 응답의 길이와 에코 시작 주소·개수를 검증합니다.</summary>
    public static void ValidateWriteMultipleRegisters(
        byte[] frame,
        ushort expectedTransactionId,
        byte expectedStation,
        ushort? expectedStartAddress = null,
        ushort? expectedQuantity = null)
    {
        ValidateHeaderAndException(frame, expectedTransactionId, expectedStation, WriteMultipleRegistersFunctionCode);
        if (frame.Length != WriteResponseFrameLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 다중 쓰기 응답 길이가 규격({WriteResponseFrameLength} 바이트)과 일치하지 않습니다. (실제: {frame.Length} 바이트)",
                frame);
        }

        ushort echoedAddress = RobotBinaryBuffer.ReadUInt16BigEndian(frame, WriteResponseAddressOffset);
        if (expectedStartAddress.HasValue && echoedAddress != expectedStartAddress.Value)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 다중 쓰기 응답 시작 주소({echoedAddress})가 요청 주소({expectedStartAddress.Value})와 일치하지 않습니다.",
                frame);
        }

        ushort echoedQuantity = RobotBinaryBuffer.ReadUInt16BigEndian(frame, WriteResponseValueOrQuantityOffset);
        if (expectedQuantity.HasValue && echoedQuantity != expectedQuantity.Value)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 다중 쓰기 응답 개수({echoedQuantity})가 요청 개수({expectedQuantity.Value})와 일치하지 않습니다.",
                frame);
        }
    }

    private static void ValidateHeaderAndException(
        byte[] frame,
        ushort expectedTransactionId,
        byte expectedStation,
        byte expectedFunctionCode)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < MinimumHeaderWithFunctionCode + 1)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus TCP 응답 길이가 최소 크기보다 짧습니다. ({frame.Length} 바이트)",
                frame);
        }

        ushort transactionId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 0);
        ushort protocolId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 2);
        ushort mbapLength = RobotBinaryBuffer.ReadUInt16BigEndian(frame, 4);
        byte station = frame[6];

        if (transactionId != expectedTransactionId || protocolId != 0 || station != expectedStation || mbapLength != frame.Length - MbapPrefixLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus MBAP 헤더 불일치입니다. (TxID: {transactionId}/{expectedTransactionId}, Station: {station}/{expectedStation}, Length: {mbapLength}/{frame.Length - MbapPrefixLength})",
                frame);
        }

        byte functionCode = frame[7];
        if (functionCode == (byte)(expectedFunctionCode | ExceptionBitMask))
        {
            byte exceptionCode = frame[8];
            throw new RobotDeviceException(
                exceptionCode,
                GetModbusExceptionMessage(exceptionCode),
                frame);
        }

        if (functionCode != expectedFunctionCode)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Modbus 기능 코드 불일치입니다. (기대: 0x{expectedFunctionCode:X2}, 수신: 0x{functionCode:X2})",
                frame);
        }
    }

    private static string GetModbusExceptionMessage(byte code) =>
        code switch
        {
            ExceptionIllegalFunction => "지원하지 않는 Modbus 기능 코드입니다. (0x01)",
            ExceptionIllegalDataAddress => "유효하지 않은 레지스터 주소입니다. (0x02)",
            ExceptionIllegalDataValue => "유효하지 않은 데이터 값입니다. (0x03)",
            ExceptionSlaveDeviceFailure => "장치 내부 오류가 발생했습니다. (0x04)",
            ExceptionSlaveDeviceBusy => "장치가 사용 중입니다. (0x06)",
            _ => $"Estun Modbus 장치 예외가 발생했습니다. (0x{code:X2})"
        };
}
