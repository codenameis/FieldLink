using System;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class ToyopucResponseParser
{
    /// <summary>단일 비트 읽기 응답을 검증하고 1바이트(0 또는 1)를 반환합니다.</summary>
    public static byte ParseReadBit(byte[] frame)
    {
        ValidateResponseHeader(frame, expectedCommand: 0x20);
        if (frame.Length < 6)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "비트 응답 데이터 길이가 부족합니다.");
        }

        return (byte)(frame[5] != 0 ? 1 : 0);
    }

    /// <summary>연속 워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 반환합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedCommand, ushort expectedWordCount)
    {
        ValidateResponseHeader(frame, expectedCommand);
        int expectedBytes = expectedWordCount * 2;
        if (frame.Length < 5 + expectedBytes)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"워드 응답 데이터 길이 부족: 필요 {5 + expectedBytes}, 수신 {frame.Length}");
        }

        byte[] result = new byte[expectedBytes];
        Array.Copy(frame, 5, result, 0, expectedBytes);
        return result;
    }

    /// <summary>쓰기 명령 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedCommand)
    {
        ValidateResponseHeader(frame, expectedCommand);
    }

    private static void ValidateResponseHeader(byte[] frame, byte expectedCommand)
    {
        if (frame is null || frame.Length < 4)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "TOYOPUC 응답 프레임 헤더 길이가 부족합니다.");
        }

        if (frame[0] != 0x80)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"올바르지 않은 TOYOPUC 응답 프레임 타입: 0x{frame[0]:X2}");
        }

        byte rc = frame[1];
        if (rc != 0x00)
        {
            byte errCode = frame.Length >= 5 ? frame[4] : rc;
            string message = GetErrorMessage(errCode);
            throw new PlcDeviceException(errCode, $"TOYOPUC 장치 오류 (코드 0x{errCode:X2}): {message}");
        }

        if (frame.Length >= 5 && frame[4] != expectedCommand)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"명령 에코 불일치: 예상 0x{expectedCommand:X2}, 수신 0x{frame[4]:X2}");
        }
    }

    private static string GetErrorMessage(byte code) => code switch
    {
        0x11 => "CPU 모듈 하드웨어 이상",
        0x20 => "중계 명령 고정 데이터 오류",
        0x21 => "전송 바이트 수 오류 (헤더-본문 길이 불일치)",
        0x23 => "정의되지 않은 명령 코드 오류",
        0x24 => "서브명령 코드 오류",
        0x25 => "명령 포맷 데이터 바이트 오류",
        0x26 => "기능 호출 피연산자 개수 오류",
        0x31 => "시퀀스 RUN 중 쓰기 금지 / 메모리 보호 오류",
        0x32 => "정의되지 않은 프로그램 번호(PRG) 오류",
        0x34 => "파라미터 설정에 의한 접근 금지",
        0x35 => "타 스테이션의 배타 제어 점유",
        0x3E => "CPU 리셋 상태로 인한 명령 실행 불가",
        0x3F => "CPU 정지(STOP) 상태로 인한 명령 실행 불가",
        0x40 => "주소 범위 초과 오류",
        0x41 => "워드 또는 바이트 개수 범위 초과 오류",
        _ => "알 수 없는 TOYOPUC 오류",
    };
}
