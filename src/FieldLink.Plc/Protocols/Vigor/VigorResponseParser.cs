using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 응답 파서입니다. (사양서 §2.1~§2.4)</summary>
public static class VigorResponseParser
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    private const int MinimumFrameLength = 7;
    private const int BodyStartOffset = 2;
    private const int TrailerLength = 4;
    private const int ChecksumHexLength = 2;

    private const int MinimumUnescapedLength = 4;
    private const int StationOffset = 0;
    private const int BodyLengthOffset = 1;
    private const int BodyHeaderLength = 3;
    private const int StatusCodeOffset = 3;
    private const int DataOffset = 4;
    private const byte StatusCodeSuccess = 0x00;

    /// <summary>VIGOR 응답 프레임을 파싱하여 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseResponse(byte[] frame, byte expectedStation)
    {
        ValidateFrameEnvelope(frame, out int etxIndex);

        byte[] unescapedBody = UnescapeBody(frame, BodyStartOffset, etxIndex - BodyStartOffset);
        VerifyChecksum(unescapedBody, frame);
        ValidateUnescapedHeader(unescapedBody, expectedStation);

        int dataLength = unescapedBody.Length - DataOffset;
        if (dataLength == 0)
        {
            return Array.Empty<byte>();
        }

        byte[] data = new byte[dataLength];
        Buffer.BlockCopy(unescapedBody, DataOffset, data, 0, dataLength);
        return data;
    }

    /// <summary>VIGOR 쓰기 응답 프레임을 검증하고 추가 데이터가 없음을 확인합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation)
    {
        byte[] data = ParseResponse(frame, expectedStation);
        if (data.Length != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"VIGOR 쓰기 응답에 예상치 못한 데이터({data.Length}바이트)가 포함되었습니다.");
        }
    }

    private static void ValidateFrameEnvelope(byte[] frame, out int etxIndex)
    {
        if (frame is null || frame.Length < MinimumFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        if (frame[0] != Dle || frame[1] != Stx)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "유효하지 않은 DLE STX 프레임 헤더입니다.");
        }

        etxIndex = FindDleEtxIndex(frame);
        if (etxIndex < 0 || etxIndex + TrailerLength != frame.Length)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "프레임 끝 DLE ETX 또는 체크섬 경계가 올바르지 않습니다.");
        }
    }

    private static int FindDleEtxIndex(byte[] frame)
    {
        int bodyEndLimit = frame.Length - ChecksumHexLength;
        for (int i = BodyStartOffset; i < bodyEndLimit - 1; i++)
        {
            if (frame[i] != Dle)
            {
                continue;
            }

            if (frame[i + 1] == Dle)
            {
                i++;
                continue;
            }

            if (frame[i + 1] == Etx)
            {
                return i;
            }

            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"VIGOR 응답 본문 내 이스케이프되지 않은 DLE(0x10)가 포함되었습니다 (다음 바이트: 0x{frame[i + 1]:X2}).");
        }

        return -1;
    }

    private static void ValidateUnescapedHeader(byte[] unescapedBody, byte expectedStation)
    {
        if (unescapedBody.Length < MinimumUnescapedLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "언이스케이프된 응답 본문 길이가 너무 짧습니다.");
        }

        byte station = unescapedBody[StationOffset];
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"국번 불일치: 요청={expectedStation}, 응답={station}");
        }

        // VS Series Communication Protocol §2.2: 본문 길이([1..2], UInt16 LE)는 상태 바이트([3])부터 데이터 끝까지의 바이트 수와 일치해야 한다.
        ushort declaredBodyLength = BinaryBuffer.ReadUInt16LittleEndian(unescapedBody, BodyLengthOffset);
        int actualRemainingLength = unescapedBody.Length - BodyHeaderLength;
        if (declaredBodyLength != actualRemainingLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"VIGOR 응답 본문 길이 불일치: 선언={declaredBodyLength}, 실제={actualRemainingLength}");
        }

        byte statusCode = unescapedBody[StatusCodeOffset];
        if (statusCode != StatusCodeSuccess)
        {
            string message = ((VigorErrorCode)statusCode).GetDescription();
            throw new PlcDeviceException(statusCode, message);
        }
    }

    private static byte[] UnescapeBody(byte[] buffer, int offset, int length)
    {
        int unescapedLength = 0;
        for (int i = 0; i < length; i++)
        {
            if (buffer[offset + i] == Dle)
            {
                if (i + 1 >= length || buffer[offset + i + 1] != Dle)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        "VIGOR 응답 본문 내 이스케이프되지 않은 DLE(0x10)가 포함되었습니다.");
                }

                i++;
            }

            unescapedLength++;
        }

        byte[] result = new byte[unescapedLength];
        int writeOffset = 0;
        for (int i = 0; i < length; i++)
        {
            byte currentByte = buffer[offset + i];
            if (currentByte == Dle)
            {
                i++;
            }

            result[writeOffset++] = currentByte;
        }

        return result;
    }

    private static void VerifyChecksum(byte[] unescapedBody, byte[] frame)
    {
        byte calculatedSum = 0;
        for (int i = 0; i < unescapedBody.Length; i++)
        {
            calculatedSum = (byte)(calculatedSum + unescapedBody[i]);
        }

        byte receivedSum = (byte)BinaryBuffer.ReadAsciiHex(frame, frame.Length - ChecksumHexLength, ChecksumHexLength);
        if (calculatedSum != receivedSum)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"VIGOR 체크섬 오류: 계산값={calculatedSum:X2}, 수신값={receivedSum:X2}");
        }
    }
}
