using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>기능 코드 및 바이트 수 필드로 Modbus RTU 응답 프레임 길이를 동적으로 판정하는 디코더입니다. (사양서 §3-2)</summary>
public sealed class ModbusRtuFrameDecoder : IFrameDecoder
{
    private const int MaximumModbusRtuLength = 256;
    private const int ExceptionFrameLength = 5;
    private const int WriteResponseFrameLength = 8;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < 2)
        {
            frameLength = 0;
            return false;
        }

        byte[] buffer = received.Array!;
        int offset = received.Offset;
        byte functionCode = buffer[offset + 1];

        // 1. 예외 응답 (FC 최상위 비트 1): 국번 1 + FC 1 + 예외코드 1 + CRC 2 = 5바이트
        if ((functionCode & 0x80) != 0)
        {
            return CheckLength(received.Count, ExceptionFrameLength, out frameLength);
        }

        // 2. 단일/다중 쓰기 정상 응답: 국번 1 + FC 1 + 주소 2 + 값/수량 2 + CRC 2 = 8바이트
        if (functionCode is (byte)ModbusFunctionCode.WriteSingleCoil
            or (byte)ModbusFunctionCode.WriteSingleRegister
            or (byte)ModbusFunctionCode.WriteMultipleCoils
            or (byte)ModbusFunctionCode.WriteMultipleRegisters)
        {
            return CheckLength(received.Count, WriteResponseFrameLength, out frameLength);
        }

        // 3. 읽기 정상 응답 (FC 0x01~0x04): 국번 1 + FC 1 + 바이트수 1 + N바이트 + CRC 2 = 5 + N바이트
        if (functionCode is (byte)ModbusFunctionCode.ReadCoils
            or (byte)ModbusFunctionCode.ReadDiscreteInputs
            or (byte)ModbusFunctionCode.ReadHoldingRegisters
            or (byte)ModbusFunctionCode.ReadInputRegisters)
        {
            if (received.Count < 3)
            {
                frameLength = 0;
                return false;
            }

            byte byteCount = buffer[offset + 2];
            int expectedLength = 5 + byteCount;

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
            $"지원하지 않는 Modbus 기능 코드(0x{functionCode:X2})입니다.");
    }

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
