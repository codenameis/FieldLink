using System;
using System.Globalization;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 응답을 검증하고 데이터를 추출하는 파서입니다. (사양서 Part A §A.2)</summary>
public static class KeyenceHostLinkResponseParser
{
    private static readonly char[] SpaceSeparator = [' '];

    /// <summary>상위 링크 통신 접속(CR) 응답이 "CC"인지 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    public static void VerifyConnectResponse(byte[] frame)
    {
        string text = ExtractText(frame);
        if (!string.Equals(text, "CC", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, $"상위 링크 연결 실패: {text}");
        }
    }

    /// <summary>쓰기 명령에 대한 응답이 "OK"인지 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    public static void VerifyWriteResponse(byte[] frame)
    {
        string text = ExtractText(frame);
        if (!string.Equals(text, "OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"상위 링크 쓰기 확인 실패: {text}");
        }
    }

    /// <summary>연속 워드 읽기 응답 문자열에서 워드 데이터 바이트(리틀 엔디언)를 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
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
            if (!ushort.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort value))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"워드 값 파싱 실패: {tokens[i]}");
            }

            BinaryBuffer.WriteUInt16LE(result, i * 2, value);
        }

        return result;
    }

    /// <summary>연속 비트 읽기 응답 문자열에서 비트 바이트 배열(0 또는 1)을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedCount">기대하는 비트 수입니다.</param>
    /// <returns>추출된 비트 바이트 배열입니다.</returns>
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
            result[i] = tokens[i] switch
            {
                "1" => 1,
                "0" => 0,
                _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 비트 응답 토큰: {tokens[i]}")
            };
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
            string errCode = full.Substring(0, 2).ToUpperInvariant();
            string message = GetErrorMessage(errCode);
            int codeNum = int.TryParse(full.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
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
