using System;
using System.Text;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part A §A.2)</summary>
public static class FujiElectricSpbResponseParser
{
    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        string ascii = ValidateAndExtractData(frame, expectedStation, "0000");
        int expectedChars = expectedCount * 4;
        if (ascii.Length < expectedChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신 데이터 길이 부족: 필요 {expectedChars}, 수신 {ascii.Length}");
        }

        byte[] result = new byte[expectedCount * 2];
        for (int i = 0; i < result.Length; i++)
        {
            string byteHex = ascii.Substring(i * 2, 2);
            if (!byte.TryParse(byteHex, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"16진 바이트 파싱 실패: {byteHex}");
            }

            result[i] = b;
        }

        return result;
    }

    /// <summary>쓰기 명령(워드 또는 비트) 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCmdSubcmd)
    {
        ValidateAndExtractData(frame, expectedStation, expectedCmdSubcmd);
    }

    private static string ValidateAndExtractData(byte[] frame, byte expectedStation, string expectedCmdSubcmd)
    {
        if (frame is null || frame.Length < 17)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != (byte)':' || frame[frame.Length - 2] != 0x0D || frame[frame.Length - 1] != 0x0A)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 프레임 헤더(:) 또는 종료(CRLF)가 올바르지 않습니다.");
        }

        string full = Encoding.ASCII.GetString(frame);
        string stationStr = full.Substring(1, 2);
        if (!byte.TryParse(stationStr, System.Globalization.NumberStyles.HexNumber, null, out byte st) || st != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 국번 불일치: 예상 {expectedStation:X2}, 수신 {stationStr}");
        }

        string endCode = full.Substring(9, 2);
        if (endCode != "00")
        {
            int errNum = int.TryParse(endCode, System.Globalization.NumberStyles.HexNumber, null, out int code) ? code : -1;
            string message = GetErrorMessage(endCode);
            throw new PlcDeviceException(errNum, $"Fuji SPB 장치 오류 (코드 {endCode}): {message}");
        }

        string cmdEcho = full.Substring(11, 4);
        if (!string.Equals(cmdEcho, expectedCmdSubcmd, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 명령 에코 불일치: 예상 {expectedCmdSubcmd}, 수신 {cmdEcho}");
        }

        // 데이터는 오프셋 15부터 CRLF 전까지
        int dataLen = (full.Length - 2) - 15;
        return dataLen > 0 ? full.Substring(15, dataLen) : string.Empty;
    }

    private static string GetErrorMessage(string code) => code switch
    {
        "01" => "ROM 영역 쓰기 요청 오류",
        "02" => "정의되지 않았거나 처리 불가한 명령/서브명령",
        "03" => "데이터 영역 매개변수 지정 오류",
        "04" => "타 로더/프로그래머의 전송 인터록 점유",
        "05" => "모듈 번호 지정 오류",
        "06" => "검색 항목 없음",
        "07" => "쓰기 주소가 모듈 메모리 범위 초과",
        "09" => "프로그램 오류로 인해 RUN 전환 불가",
        "0C" => "암호 불일치",
        _ => "알 수 없는 Fuji SPB 오류",
    };
}
