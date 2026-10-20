using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 기능 코드(0x1E~0x21) 및 표준 Modbus RTU 응답 프레임 길이를 판정하는 디코더입니다. (사양서 Part A §A.2)</summary>
public sealed class XinjeRtuFrameDecoder : IFrameDecoder
{
    private const int MaximumFrameLength = 512;
    private const int ExceptionFrameLength = 5;
    private const int ExtendedWriteResponseFrameLength = 10; // 국번 1 + FC 1 + 디바이스코드 1 + 주소 3 + 개수 2 + CRC 2

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

        // 1. 예외 응답 (FC 최상위 비트 1): 5바이트
        if ((functionCode & 0x80) != 0)
        {
            return CheckLength(received.Count, ExceptionFrameLength, out frameLength);
        }

        // 2. 확장 쓰기 정상 응답 (0x1F 비트 쓰기, 0x21 워드 쓰기): 10바이트
        if (functionCode is 0x1F or 0x21)
        {
            return CheckLength(received.Count, ExtendedWriteResponseFrameLength, out frameLength);
        }

        // 3. 표준 쓰기 응답 (0x05, 0x06, 0x0F, 0x10): 8바이트
        if (functionCode is 0x05 or 0x06 or 0x0F or 0x10)
        {
            return CheckLength(received.Count, 8, out frameLength);
        }

        // 4. 읽기 응답 (0x1E, 0x20 확장 읽기 또는 0x01~0x04 표준 읽기): 국번 1 + FC 1 + 바이트수 1 + N + CRC 2
        if (functionCode is 0x1E or 0x20 or 0x01 or 0x02 or 0x03 or 0x04)
        {
            if (received.Count < 3)
            {
                frameLength = 0;
                return false;
            }

            int byteCount = buffer[offset + 2];
            int expected = 5 + byteCount;
            return CheckLength(received.Count, expected, out frameLength);
        }

        frameLength = 0;
        return false;
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
