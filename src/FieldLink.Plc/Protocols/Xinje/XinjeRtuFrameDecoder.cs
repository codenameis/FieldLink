using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 기능 코드(0x1E~0x21) 및 표준 Modbus RTU 응답 프레임 길이를 판정하는 디코더입니다. (사양서 Part A §A.2)</summary>
public sealed class XinjeRtuFrameDecoder : IFrameDecoder
{
    private const int MaximumFrameLength = 512;
    private const int MinimumHeaderLength = 2;
    private const int ReadByteCountHeaderLength = 3;
    private const int ReadFrameOverheadLength = 5;
    private const int ExceptionFrameLength = 5;
    private const int StandardWriteResponseFrameLength = 8;
    private const int ExtendedWriteResponseFrameLength = 10; // 국번 1 + FC 1 + 디바이스코드 1 + 주소 3 + 개수 2 + CRC 2
    private const byte ExceptionFunctionMask = 0x80;
    private const byte ExtendedReadBitsFc = 0x1E;
    private const byte ExtendedWriteBitsFc = 0x1F;
    private const byte ExtendedReadWordsFc = 0x20;
    private const byte ExtendedWriteWordsFc = 0x21;
    private const byte StandardWriteSingleCoilFc = 0x05;
    private const byte StandardWriteSingleRegisterFc = 0x06;
    private const byte StandardWriteMultipleCoilsFc = 0x0F;
    private const byte StandardWriteMultipleRegistersFc = 0x10;
    private const byte StandardReadCoilsFc = 0x01;
    private const byte StandardReadDiscreteInputsFc = 0x02;
    private const byte StandardReadHoldingRegistersFc = 0x03;
    private const byte StandardReadInputRegistersFc = 0x04;

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

        // 1. 예외 응답 (FC 최상위 비트 1): 5바이트
        if ((functionCode & ExceptionFunctionMask) != 0)
        {
            return CheckLength(received.Count, ExceptionFrameLength, out frameLength);
        }

        // 2. 확장 쓰기 정상 응답 (0x1F 비트 쓰기, 0x21 워드 쓰기): 10바이트
        if (functionCode is ExtendedWriteBitsFc or ExtendedWriteWordsFc)
        {
            return CheckLength(received.Count, ExtendedWriteResponseFrameLength, out frameLength);
        }

        // 3. 표준 쓰기 응답 (0x05, 0x06, 0x0F, 0x10): 8바이트
        if (functionCode is StandardWriteSingleCoilFc or StandardWriteSingleRegisterFc or StandardWriteMultipleCoilsFc or StandardWriteMultipleRegistersFc)
        {
            return CheckLength(received.Count, StandardWriteResponseFrameLength, out frameLength);
        }

        // 4. 읽기 응답 (0x1E, 0x20 확장 읽기 또는 0x01~0x04 표준 읽기): 국번 1 + FC 1 + 바이트수 1 + N + CRC 2
        if (functionCode is ExtendedReadBitsFc or ExtendedReadWordsFc or StandardReadCoilsFc or StandardReadDiscreteInputsFc or StandardReadHoldingRegistersFc or StandardReadInputRegistersFc)
        {
            if (received.Count < ReadByteCountHeaderLength)
            {
                frameLength = 0;
                return false;
            }

            int byteCount = buffer[offset + 2];
            int expected = ReadFrameOverheadLength + byteCount;
            return CheckLength(received.Count, expected, out frameLength);
        }

        throw new TransportException(
            TransportFailure.InvalidFrame,
            $"지원하지 않는 XINJE Modbus 기능 코드(0x{functionCode:X2})입니다.");
    }

    private static bool CheckLength(int actualLength, int expectedLength, out int frameLength)
    {
        if (expectedLength > MaximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"예상 프레임 길이({expectedLength})가 최대 허용 크기를 초과했습니다.");
        }

        if (actualLength >= expectedLength)
        {
            frameLength = expectedLength;
            return true;
        }

        frameLength = 0;
        return false;
    }
}
