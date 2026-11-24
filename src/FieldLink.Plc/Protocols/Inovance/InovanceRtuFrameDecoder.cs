using System;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance 표준 및 확장 기능 코드(FC + 0x30) Modbus RTU 응답 프레임의 경계를 판정하는 디코더입니다. (사양서 §1.3)</summary>
public sealed class InovanceRtuFrameDecoder : IFrameDecoder
{
    private const int MaximumModbusRtuLength = 256;
    private const int MinimumHeaderLength = 2;
    private const int ReadByteCountHeaderLength = 3;
    private const int ReadFrameOverheadLength = 5;
    private const byte ExceptionFunctionMask = 0x80;
    private const int ExceptionFrameLength = 5;
    private const int WriteResponseFrameLength = 8;

    private const byte ExtendedReadCoilsFunctionCode = (byte)ModbusFunctionCode.ReadCoils + InovanceRequestBuilder.ExtendedFunctionCodeOffset;
    private const byte ExtendedReadHoldingRegistersFunctionCode = (byte)ModbusFunctionCode.ReadHoldingRegisters + InovanceRequestBuilder.ExtendedFunctionCodeOffset;
    private const byte ExtendedWriteSingleCoilFunctionCode = (byte)ModbusFunctionCode.WriteSingleCoil + InovanceRequestBuilder.ExtendedFunctionCodeOffset;
    private const byte ExtendedWriteSingleRegisterFunctionCode = (byte)ModbusFunctionCode.WriteSingleRegister + InovanceRequestBuilder.ExtendedFunctionCodeOffset;
    private const byte ExtendedWriteMultipleCoilsFunctionCode = (byte)ModbusFunctionCode.WriteMultipleCoils + InovanceRequestBuilder.ExtendedFunctionCodeOffset;
    private const byte ExtendedWriteMultipleRegistersFunctionCode = (byte)ModbusFunctionCode.WriteMultipleRegisters + InovanceRequestBuilder.ExtendedFunctionCodeOffset;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < MinimumHeaderLength || received.Array is not { } buffer)
        {
            frameLength = 0;
            return false;
        }

        int offset = received.Offset;
        byte functionCode = buffer[offset + 1];

        if ((functionCode & ExceptionFunctionMask) != 0)
        {
            return CheckLength(received.Count, ExceptionFrameLength, out frameLength);
        }

        if (IsWriteResponseFunctionCode(functionCode))
        {
            return CheckLength(received.Count, WriteResponseFrameLength, out frameLength);
        }

        if (IsReadResponseFunctionCode(functionCode))
        {
            if (received.Count < ReadByteCountHeaderLength)
            {
                frameLength = 0;
                return false;
            }

            byte byteCount = buffer[offset + 2];
            int expectedLength = ReadFrameOverheadLength + byteCount;
            if (expectedLength > MaximumModbusRtuLength)
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    $"계산된 RTU 프레임 길이({expectedLength} 바이트)가 최대 허용치({MaximumModbusRtuLength})를 초과합니다.");
            }

            return CheckLength(received.Count, expectedLength, out frameLength);
        }

        throw new TransportException(
            TransportFailure.InvalidFrame,
            $"지원하지 않는 Inovance Modbus 기능 코드(0x{functionCode:X2})입니다.");
    }

    private static bool IsWriteResponseFunctionCode(byte functionCode) =>
        functionCode is (byte)ModbusFunctionCode.WriteSingleCoil
            or (byte)ModbusFunctionCode.WriteSingleRegister
            or (byte)ModbusFunctionCode.WriteMultipleCoils
            or (byte)ModbusFunctionCode.WriteMultipleRegisters
            or ExtendedWriteSingleCoilFunctionCode
            or ExtendedWriteSingleRegisterFunctionCode
            or ExtendedWriteMultipleCoilsFunctionCode
            or ExtendedWriteMultipleRegistersFunctionCode;

    private static bool IsReadResponseFunctionCode(byte functionCode) =>
        functionCode is (byte)ModbusFunctionCode.ReadCoils
            or (byte)ModbusFunctionCode.ReadDiscreteInputs
            or (byte)ModbusFunctionCode.ReadHoldingRegisters
            or (byte)ModbusFunctionCode.ReadInputRegisters
            or ExtendedReadCoilsFunctionCode
            or ExtendedReadHoldingRegistersFunctionCode;

    private static bool CheckLength(int receivedCount, int expectedLength, out int frameLength)
    {
        if (receivedCount < expectedLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = expectedLength;
        return true;
    }
}
