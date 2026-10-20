using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 응답 프레임을 검증하고 데이터를 추출하는 파서입니다. (사양서 §2)</summary>
public static class FatekResponseParser
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>비트 읽기(명령 44) 응답을 검증하고 비트 배열을 추출합니다.</summary>
    public static byte[] ParseReadBits(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        string body = ValidateAndExtractBody(frame, expectedStation, "44");
        if (body.Length < expectedCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신된 비트 응답 길이가 부족합니다. (필요: {expectedCount}, 수신: {body.Length})");
        }

        byte[] result = new byte[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            result[i] = (byte)(body[i] == '1' ? 1 : 0);
        }

        return result;
    }

    /// <summary>워드 읽기(명령 46) 응답을 검증하고 16비트 리틀엔디언 바이트 배열을 추출합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        string body = ValidateAndExtractBody(frame, expectedStation, "46");
        int requiredChars = expectedCount * 4;
        if (body.Length < requiredChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신된 워드 응답 길이가 부족합니다. (필요: {requiredChars}, 수신: {body.Length})");
        }

        byte[] result = new byte[expectedCount * 2];
        for (int i = 0; i < expectedCount; i++)
        {
            string hex4 = body.Substring(i * 4, 4);
            if (!ushort.TryParse(hex4, System.Globalization.NumberStyles.HexNumber, null, out ushort value))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"16진 워드 파싱 실패: {hex4}");
            }

            // 리틀엔디언 저장 [lo, hi]
            result[i * 2] = (byte)(value & 0xFF);
            result[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return result;
    }

    /// <summary>쓰기 명령(명령 45, 47, 41) 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCommand)
    {
        ValidateAndExtractBody(frame, expectedStation, expectedCommand);
    }

    private static string ValidateAndExtractBody(byte[] frame, byte expectedStation, string expectedCommand)
    {
        if (frame is null || frame.Length < 9)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != Stx || frame[frame.Length - 1] != Etx)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 시작(STX) 또는 종료(ETX) 문자가 올바르지 않습니다.");
        }

        ValidateChecksum(frame);

        string stationHex = Encoding.ASCII.GetString(frame, 1, 2);
        if (!byte.TryParse(stationHex, System.Globalization.NumberStyles.HexNumber, null, out byte st) || st != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"국번 불일치: 예상 {expectedStation:X2}, 수신 {stationHex}");
        }

        string command = Encoding.ASCII.GetString(frame, 3, 2);
        if (!string.Equals(command, expectedCommand, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"명령 에코 불일치: 예상 {expectedCommand}, 수신 {command}");
        }

        char status = (char)frame[5];
        if (status != '0')
        {
            string message = GetErrorMessage(status);
            throw new PlcDeviceException((int)status, $"FATEK 장치 오류 (코드 '{status}'): {message}");
        }

        int dataLength = frame.Length - 9; // 전체 - STX(1) - 국번(2) - 명령(2) - 오류코드(1) - 체크섬(2) - ETX(1)
        return dataLength > 0 ? Encoding.ASCII.GetString(frame, 6, dataLength) : string.Empty;
    }

    private static void ValidateChecksum(byte[] frame)
    {
        int sum = Stx;
        for (int i = 1; i < frame.Length - 3; i++)
        {
            sum += frame[i];
        }

        byte expectedLrc = (byte)(sum & 0xFF);
        string lrcStr = Encoding.ASCII.GetString(frame, frame.Length - 3, 2);
        if (!byte.TryParse(lrcStr, System.Globalization.NumberStyles.HexNumber, null, out byte actualLrc) || expectedLrc != actualLrc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FATEK 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {lrcStr}");
        }
    }

    private static string GetErrorMessage(char code) => code switch
    {
        '2' => "데이터 값 범위 초과 (Illegal value)",
        '3' => "포맷 오류 또는 쓰기 금지 / 명령 실행 불가 (Illegal format / write protected)",
        '4' => "정의되지 않은 명령 코드 (Invalid command code)",
        '5' => "RUN 전환 불가 — 래더 체크섬 불일치",
        '6' => "RUN 전환 불가 — PLC ID와 래더 ID 불일치",
        '7' => "RUN 전환 불가 — 래더 문법 오류",
        '9' => "RUN 전환 불가 — 미지원 명령어 포함",
        'A' => "컴포넌트 기호 또는 주소 범위 초과 (Illegal address)",
        _ => "알 수 없는 FATEK 오류",
    };
}
