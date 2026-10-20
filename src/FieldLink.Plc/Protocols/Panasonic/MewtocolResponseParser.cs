using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class MewtocolResponseParser
{
    /// <summary>단일 비트 읽기(RC) 응답을 검증하고 비트 값(0 또는 1)을 반환합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <returns>읽어온 비트 값(0 또는 1)입니다.</returns>
    public static byte ParseReadSingleBit(byte[] frame, byte expectedStation)
    {
        int dataLen = ValidateHeaderAndBcc(frame, expectedStation, "RC");
        if (dataLen < 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "비트 응답 데이터가 비어 있습니다.");
        }

        return frame[6] switch
        {
            (byte)'1' => 1,
            (byte)'0' => 0,
            _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 비트 응답 문자입니다: 0x{frame[6]:X2}")
        };
    }

    /// <summary>연속 워드 읽기(RD) 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedWordCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedWordCount)
    {
        int dataLen = ValidateHeaderAndBcc(frame, expectedStation, "RD");
        int expectedChars = expectedWordCount * 4;
        if (dataLen < expectedChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신 데이터 길이 부족: 필요 {expectedChars}, 수신 {dataLen}");
        }

        byte[] result = new byte[expectedWordCount * 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)BinaryBuffer.ReadAsciiHex(frame, 6 + (i * 2), 2);
        }

        return result;
    }

    /// <summary>쓰기 명령 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedEcho">기대하는 2글자 명령 에코(예: "WC", "WD")입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedEcho)
    {
        ValidateHeaderAndBcc(frame, expectedStation, expectedEcho);
    }

    private static int ValidateHeaderAndBcc(byte[] frame, byte expectedStation, string expectedEcho)
    {
        if (frame is null || frame.Length < 9)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != (byte)'%' && frame[0] != (byte)'<')
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 시작 헤더('%' 또는 '<')가 올바르지 않습니다: 0x{frame[0]:X2}");
        }

        if (frame[frame.Length - 1] != 0x0D)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 종료자(CR)가 없습니다.");
        }

        ValidateBcc(frame);

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, 1, 2);
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        char status = (char)frame[3];
        if (status == '!')
        {
            string errCode = Encoding.ASCII.GetString(frame, 4, 2);
            int codeVal = int.TryParse(errCode, out int n) ? n : -1;
            string message = GetErrorMessage(errCode);
            throw new PlcDeviceException(codeVal, $"MEWTOCOL 에러 (코드 {errCode}): {message}");
        }

        if (status != '$')
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"알 수 없는 MEWTOCOL 상태 마커: {status}");
        }

        string cmdEcho = Encoding.ASCII.GetString(frame, 4, 2);
        if (!string.Equals(cmdEcho, expectedEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 명령 에코 불일치: 예상 {expectedEcho}, 수신 {cmdEcho}");
        }

        return (frame.Length - 3) - 6;
    }

    private static void ValidateBcc(byte[] frame)
    {
        byte calculatedBcc = 0;
        for (int i = 0; i < frame.Length - 3; i++)
        {
            calculatedBcc ^= frame[i];
        }

        byte actualBcc = (byte)BinaryBuffer.ReadAsciiHex(frame, frame.Length - 3, 2);
        if (calculatedBcc != actualBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL BCC 불일치: 계산 {calculatedBcc:X2}, 수신 {actualBcc:X2}");
        }
    }

    private static string GetErrorMessage(string code) => code switch
    {
        "20" => "링크 알 수 없는 오류",
        "21" => "NACK 오류 (전송 패리티/프레이밍 오류)",
        "22" => "WACK 오류 (수신 버퍼 오버플로)",
        "24" => "전송 포맷 오류",
        "27" => "프레임 길이 초과 오류",
        "40" => "BCC 체크섬 오류",
        "41" => "포맷 오류 (명령 길이/형식 불일치)",
        "42" => "미지원 명령 오류 (NOT SUPPORT)",
        "43" => "절차 오류",
        "50" => "링크 설정 오류",
        "60" => "파라미터 오류 (영역 코드 부재)",
        "61" => "데이터 오류 (접점/레지스터 범위 초과)",
        "65" => "보호 오류 (쓰기 금지/잠금)",
        "66" => "주소 오류",
        _ => "알 수 없는 MEWTOCOL 오류",
    };
}
