using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 응답을 검증하고 데이터를 추출하는 파서입니다. (사양서 Part A §A.2)</summary>
public static class KeyenceHostLinkResponseParser
{
    private static readonly char[] SpaceSeparator = [' '];

    /// <summary>연결 개시(CR) 응답("CC\r\n")을 검증합니다.</summary>
    public static void VerifyConnectResponse(byte[] frame)
    {
        string text = ExtractText(frame);
        if (!string.Equals(text, "CC", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, $"상위 링크 연결 실패: {text}");
        }
    }

    /// <summary>쓰기 명령(WRS) 응답("OK\r\n")을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame)
    {
        string text = ExtractText(frame);
        if (!string.Equals(text, "OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"상위 링크 쓰기 확인 실패: {text}");
        }
    }

    /// <summary>워드 읽기(RDS) 응답에서 공백 구분 16비트 워드를 추출하여 리틀엔디언 바이트로 반환합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, ushort expectedCount)
    {
        string text = ExtractText(frame);
        string[] tokens = text.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < expectedCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"워드 응답 토큰 수 부족 (예상: {expectedCount}, 수신: {tokens.Length})");
        }

        byte[] result = new byte[expectedCount * 2];
        for (int i = 0; i < expectedCount; i++)
        {
            if (!ushort.TryParse(tokens[i], out ushort value))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"워드 값 파싱 실패: {tokens[i]}");
            }

            result[i * 2] = (byte)(value & 0xFF);
            result[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return result;
    }

    /// <summary>비트 읽기(RDS) 응답에서 공백 구분 비트 상태를 추출합니다.</summary>
    public static byte[] ParseReadBits(byte[] frame, ushort expectedCount)
    {
        string text = ExtractText(frame);
        string[] tokens = text.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < expectedCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"비트 응답 토큰 수 부족 (예상: {expectedCount}, 수신: {tokens.Length})");
        }

        byte[] result = new byte[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            result[i] = (byte)(tokens[i] == "1" ? 1 : 0);
        }

        return result;
    }

    private static string ExtractText(byte[] frame)
    {
        if (frame is null || frame.Length < 2)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "응답 프레임이 너무 짧습니다.");
        }

        string full = Encoding.ASCII.GetString(frame).TrimEnd('\r', '\n');
        if (full.StartsWith("E", StringComparison.OrdinalIgnoreCase) && full.Length >= 2)
        {
            string errCode = full.Substring(0, 2);
            string message = GetErrorMessage(errCode);
            int codeNum = int.TryParse(full.Substring(1), out int n) ? n : -1;
            throw new PlcDeviceException(codeNum, $"상위 링크 에러 ({errCode}): {message}");
        }

        return full;
    }

    private static string GetErrorMessage(string code) => code switch
    {
        "E0" => "디바이스 번호 이상 (범위 초과 또는 미등록 디바이스)",
        "E1" => "커맨드 이상 (미지원 명령 또는 파라미터 형식 오류)",
        "E2" => "프로그램 미등록 (CPU에 프로그램 부재 또는 모드 오류)",
        "E4" => "쓰기 금지 (메모리 쓰기 금지 상태)",
        "E5" => "본체 에러 (CPU 유닛 에러 미해제)",
        "E6" => "코멘트 없음",
        _ => "알 수 없는 Keyence 상위 링크 에러",
    };
}
