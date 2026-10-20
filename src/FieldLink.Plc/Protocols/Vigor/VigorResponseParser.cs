using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 응답 파서입니다. (사양서 §2.1~§2.4)</summary>
public static class VigorResponseParser
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>VIGOR 응답 프레임을 파싱하여 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseResponse(byte[] frame, byte expectedStation)
    {
        if (frame is null || frame.Length < 7)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        if (frame[0] != Dle || frame[1] != Stx)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "유효하지 않은 DLE STX 프레임 헤더입니다.");
        }

        int etxIndex = -1;
        for (int i = 2; i < frame.Length - 1; i++)
        {
            if (frame[i] == Dle)
            {
                if (frame[i + 1] == Dle)
                {
                    i++;
                    continue;
                }

                if (frame[i + 1] == Etx)
                {
                    etxIndex = i;
                    break;
                }
            }
        }

        if (etxIndex < 0 || etxIndex + 4 != frame.Length)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "프레임 끝 DLE ETX 또는 체크섬 경계가 올바르지 않습니다.");
        }

        byte[] unescaped = UnescapeBody(frame, 2, etxIndex - 2);
        VerifyChecksum(unescaped, frame);

        if (unescaped.Length < 4)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "언이스케이프된 응답 본문 길이가 너무 짧습니다.");
        }

        byte station = unescaped[0];
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"국번 불일치: 요청={expectedStation}, 응답={station}");
        }

        byte statusCode = unescaped[3];
        if (statusCode != 0x00)
        {
            throw new PlcDeviceException(statusCode, FormatErrorMessage(statusCode));
        }

        int dataLen = unescaped.Length - 4;
        if (dataLen == 0)
        {
            return Array.Empty<byte>();
        }

        byte[] data = new byte[dataLen];
        Buffer.BlockCopy(unescaped, 4, data, 0, dataLen);
        return data;
    }

    private static byte[] UnescapeBody(byte[] buffer, int offset, int length)
    {
        int unescapedLen = 0;
        for (int i = 0; i < length; i++)
        {
            if (buffer[offset + i] == Dle && i + 1 < length && buffer[offset + i + 1] == Dle)
            {
                i++;
            }

            unescapedLen++;
        }

        byte[] result = new byte[unescapedLen];
        int dst = 0;
        for (int i = 0; i < length; i++)
        {
            byte b = buffer[offset + i];
            if (b == Dle && i + 1 < length && buffer[offset + i + 1] == Dle)
            {
                i++;
            }

            result[dst++] = b;
        }

        return result;
    }

    private static void VerifyChecksum(byte[] unescaped, byte[] frame)
    {
        byte sum = 0;
        for (int i = 0; i < unescaped.Length; i++)
        {
            sum = (byte)(sum + unescaped[i]);
        }

        byte actualSum = (byte)BinaryBuffer.ReadAsciiHex(frame, frame.Length - 2, 2);
        if (sum != actualSum)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"VIGOR 체크섬 오류: 계산값={sum:X2}, 수신값={actualSum:X2}");
        }
    }

    private static string FormatErrorMessage(byte code) => code switch
    {
        0x02 => "체크섬(Sum Check) 오류입니다. (0x02)",
        0x04 => "요청 데이터 개수가 0이거나 허용 블록 크기를 초과했습니다. (0x04)",
        0x06 => "디바이스 주소 범위 초과 오류입니다. (0x06)",
        0x08 => "ASCII 변환 오류입니다. (0x08)",
        0x31 => "존재하지 않거나 지원하지 않는 기능 코드입니다. (0x31)",
        _ => $"VIGOR 오류 코드: 0x{code:X2}",
    };
}
