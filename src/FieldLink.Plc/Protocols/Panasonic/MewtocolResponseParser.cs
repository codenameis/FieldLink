using System;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class MewtocolResponseParser
{
    /// <summary>단일 접점 읽기(#RCS) 응답을 검증하고 1바이트(0 또는 1)를 반환합니다.</summary>
    public static byte ParseReadSingleBit(byte[] frame, byte expectedStation)
    {
        string data = ValidateAndExtractData(frame, expectedStation, "RC");
        if (data.Length < 1)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "비트 응답 데이터가 비어 있습니다.");
        }

        return (byte)(data[0] == '1' ? 1 : 0);
    }

    /// <summary>연속 워드 읽기(#RD) 응답을 검증하고 리틀엔디언 바이트 배열을 반환합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedWordCount)
    {
        string data = ValidateAndExtractData(frame, expectedStation, "RD");
        int expectedChars = expectedWordCount * 4;
        if (data.Length < expectedChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신 데이터 길이 부족: 필요 {expectedChars}, 수신 {data.Length}");
        }

        byte[] result = new byte[expectedWordCount * 2];
        for (int i = 0; i < result.Length; i++)
        {
            string hexByte = data.Substring(i * 2, 2);
            if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"16진 바이트 파싱 실패: {hexByte}");
            }

            result[i] = b;
        }

        return result;
    }

    /// <summary>쓰기 명령(#WCS, #WD) 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedEcho)
    {
        ValidateAndExtractData(frame, expectedStation, expectedEcho);
    }

    private static string ValidateAndExtractData(byte[] frame, byte expectedStation, string expectedEcho)
    {
        if (frame is null || frame.Length < 9)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[frame.Length - 1] != 0x0D)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 종료자(CR)가 없습니다.");
        }

        ValidateBcc(frame);

        string full = Encoding.ASCII.GetString(frame);
        string stationStr = full.Substring(1, 2);
        if (!byte.TryParse(stationStr, System.Globalization.NumberStyles.HexNumber, null, out byte st) || st != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 국번 불일치: 예상 {expectedStation:X2}, 수신 {stationStr}");
        }

        char status = full[3];
        if (status == '!')
        {
            string errCode = full.Substring(4, 2);
            int codeVal = int.TryParse(errCode, out int n) ? n : -1;
            string message = GetErrorMessage(errCode);
            throw new PlcDeviceException(codeVal, $"MEWTOCOL 에러 (코드 {errCode}): {message}");
        }

        if (status != '$')
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"알 수 없는 MEWTOCOL 상태 마커: {status}");
        }

        string cmdEcho = full.Substring(4, 2);
        if (!string.Equals(cmdEcho, expectedEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 명령 에코 불일치: 예상 {expectedEcho}, 수신 {cmdEcho}");
        }

        // 데이터는 오프셋 6부터 BCC(2자리) 및 CR(1자리) 직전까지
        int dataLen = (full.Length - 3) - 6;
        return dataLen > 0 ? full.Substring(6, dataLen) : string.Empty;
    }

    private static void ValidateBcc(byte[] frame)
    {
        byte calculatedBcc = 0;
        for (int i = 0; i < frame.Length - 3; i++)
        {
            calculatedBcc ^= frame[i];
        }

        string bccStr = Encoding.ASCII.GetString(frame, frame.Length - 3, 2);
        if (!byte.TryParse(bccStr, System.Globalization.NumberStyles.HexNumber, null, out byte actualBcc) || calculatedBcc != actualBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL BCC 불일치: 계산 {calculatedBcc:X2}, 수신 {bccStr}");
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
