using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 응답 파서입니다. (사양서 §2)</summary>
public static class YokogawaResponseParser
{
    private const int HeaderLength = 4;
    private const int SubheaderOffset = 0;
    private const int EndCodeOffset = 1;
    private const int SizeFieldOffset = 2;
    private const byte ResponseFlagMask = 0x80;
    private const byte EndCodeSuccess = 0x00;

    /// <summary>읽기 응답 프레임에서 데이터 바이트를 추출합니다.</summary>
    public static byte[] ParseReadResponse(byte[] frame, byte expectedCommand)
    {
        int payloadSize = ValidateHeader(frame, expectedCommand);
        byte[] data = new byte[payloadSize];
        Buffer.BlockCopy(frame, HeaderLength, data, 0, payloadSize);
        return data;
    }

    /// <summary>쓰기 응답 프레임의 성공 여부를 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedCommand)
    {
        ValidateHeader(frame, expectedCommand);
    }

    private static int ValidateHeader(byte[] frame, byte expectedCommand)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 길이가 너무 짧습니다: {frame?.Length ?? 0}바이트");
        }

        byte expectedSubheader = (byte)(expectedCommand | ResponseFlagMask);
        if (frame[SubheaderOffset] != expectedSubheader)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"서브헤더 응답 불일치: 기대=0x{expectedSubheader:X2}, 수신=0x{frame[SubheaderOffset]:X2}");
        }

        // IM 34M06H24-08E §5.3.2 / 사양서 §2.2 4단계: 선언된 크기와 실제 본문 길이(frame.Length - 4)가 다르면 손상 프레임으로 처리한다.
        int declaredSize = BinaryBuffer.ReadUInt16BigEndian(frame, SizeFieldOffset);
        if (frame.Length != HeaderLength + declaredSize)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"응답 프레임 길이가 선언된 크기와 일치하지 않습니다: 선언={declaredSize}, 실제 본문={frame.Length - HeaderLength}");
        }

        byte endCode = frame[EndCodeOffset];
        if (endCode != EndCodeSuccess)
        {
            throw new PlcDeviceException(endCode, FormatErrorMessage(endCode));
        }

        return declaredSize;
    }

    private static string FormatErrorMessage(byte endCode) => endCode switch
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
