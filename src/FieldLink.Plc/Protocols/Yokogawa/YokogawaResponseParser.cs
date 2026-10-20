using System;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 응답 파서입니다. (사양서 §2)</summary>
public static class YokogawaResponseParser
{
    /// <summary>읽기 응답 프레임에서 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseReadResponse(byte[] frame, byte expectedCommand)
    {
        if (frame is null || frame.Length < 4)
        {
            throw new InvalidOperationException($"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        byte expectedSubheader = (byte)(expectedCommand | 0x80);
        if (frame[0] != expectedSubheader)
        {
            throw new InvalidOperationException($"서브헤더 응답 불일치: 기대=0x{expectedSubheader:X2}, 수신=0x{frame[0]:X2}");
        }

        byte endCode = frame[1];
        if (endCode != 0x00)
        {
            throw new InvalidOperationException(FormatErrorMessage(endCode));
        }

        int size = (frame[2] << 8) | frame[3];
        if (frame.Length < 4 + size)
        {
            throw new InvalidOperationException($"응답 데이터가 선언된 크기({size})보다 적습니다.");
        }

        byte[] data = new byte[size];
        Array.Copy(frame, 4, data, 0, size);
        return data;
    }

    /// <summary>쓰기 응답 프레임의 성공 여부를 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedCommand)
    {
        if (frame is null || frame.Length < 4)
        {
            throw new InvalidOperationException($"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        byte expectedSubheader = (byte)(expectedCommand | 0x80);
        if (frame[0] != expectedSubheader)
        {
            throw new InvalidOperationException($"서브헤더 응답 불일치: 기대=0x{expectedSubheader:X2}, 수신=0x{frame[0]:X2}");
        }

        byte endCode = frame[1];
        if (endCode != 0x00)
        {
            throw new InvalidOperationException(FormatErrorMessage(endCode));
        }
    }

    private static string FormatErrorMessage(byte endCode)
    {
        return endCode switch
        {
            0x01 => "CPU 번호 오류입니다. (1~4 범위 초과, ER01)",
            0x02 => "명령 오류입니다. (명령 없음 또는 실행 불가, ER02)",
            0x03 => "디바이스 오류입니다. (해당 디바이스 없음, ER03)",
            0x04 => "설정값 범위 오류입니다. (ER04)",
            0x05 => "데이터 개수 범위 오류입니다. (ER05)",
            0x06 => "모니터 오류입니다. (ER06)",
            0x08 => "파라미터 오류입니다. (ER08)",
            0x09 => "ID 오류입니다. (ER09)",
            0x51 => "시퀀스 CPU 무응답(타임아웃) 오류입니다. (ER51)",
            0x52 => "시퀀스 CPU 처리 오류입니다. (ER52)",
            _ => $"Yokogawa 종료 코드 오류: 0x{endCode:X2}",
        };
    }
}
