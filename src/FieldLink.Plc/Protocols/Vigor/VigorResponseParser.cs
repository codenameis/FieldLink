using System;
using System.IO;

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
            throw new InvalidOperationException($"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        if (frame[0] != Dle || frame[1] != Stx)
        {
            throw new InvalidOperationException("유효하지 않은 DLE STX 프레임 헤더입니다.");
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
            throw new InvalidOperationException("프레임 끝 DLE ETX 또는 체크섬 경계가 올바르지 않습니다.");
        }

        byte[] unescaped = UnescapeBody(frame, 2, etxIndex - 2);
        VerifyChecksum(unescaped, frame[frame.Length - 2], frame[frame.Length - 1]);

        if (unescaped.Length < 4)
        {
            throw new InvalidOperationException("언이스케이프된 응답 본문 길이가 너무 짧습니다.");
        }

        byte station = unescaped[0];
        if (station != expectedStation)
        {
            throw new InvalidOperationException($"국번 불일치: 요청={expectedStation}, 응답={station}");
        }

        byte statusCode = unescaped[3];
        if (statusCode != 0x00)
        {
            throw new InvalidOperationException(FormatErrorMessage(statusCode));
        }

        int dataLen = unescaped.Length - 4;
        byte[] data = new byte[dataLen];
        if (dataLen > 0)
        {
            Array.Copy(unescaped, 4, data, 0, dataLen);
        }

        return data;
    }

    private static byte[] UnescapeBody(byte[] buffer, int offset, int length)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            for (int i = 0; i < length; i++)
            {
                byte b = buffer[offset + i];
                if (b == Dle && i + 1 < length && buffer[offset + i + 1] == Dle)
                {
                    ms.WriteByte(Dle);
                    i++;
                }
                else
                {
                    ms.WriteByte(b);
                }
            }

            return ms.ToArray();
        }
    }

    private static void VerifyChecksum(byte[] unescaped, byte c0, byte c1)
    {
        byte sum = 0;
        foreach (byte b in unescaped)
        {
            sum = (byte)(sum + b);
        }

        string expectedHex = sum.ToString("X2");
        if ((byte)expectedHex[0] != c0 || (byte)expectedHex[1] != c1)
        {
            char r0 = (char)c0;
            char r1 = (char)c1;
            throw new InvalidOperationException($"VIGOR 체크섬 오류: 계산값={expectedHex}, 수신값={r0}{r1}");
        }
    }

    private static string FormatErrorMessage(byte code)
    {
        return code switch
        {
            0x02 => "체크섬(Sum Check) 오류입니다. (0x02)",
            0x04 => "요청 데이터 개수가 0이거나 허용 블록 크기를 초과했습니다. (0x04)",
            0x06 => "디바이스 주소 범위 초과 오류입니다. (0x06)",
            0x08 => "ASCII 변환 오류입니다. (0x08)",
            0x31 => "존재하지 않거나 지원하지 않는 기능 코드입니다. (0x31)",
            _ => $"VIGOR 오류 코드: 0x{code:X2}",
        };
    }
}
