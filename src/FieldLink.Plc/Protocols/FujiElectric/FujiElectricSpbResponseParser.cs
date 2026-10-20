using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part A §A.2)</summary>
public static class FujiElectricSpbResponseParser
{
    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        int dataOffset = ValidateHeaderAndLrc(frame, expectedStation, "0000", out int availableDataChars);
        int expectedChars = expectedCount * 4;

        if (availableDataChars < expectedChars)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신 데이터 길이 부족: 필요 {expectedChars}, 수신 {availableDataChars}");
        }

        byte[] result = new byte[expectedCount * 2];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)BinaryBuffer.ReadAsciiHex(frame, dataOffset + (i * 2), 2);
        }

        return result;
    }

    /// <summary>쓰기 명령(워드 또는 비트) 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCmdSubcmd)
    {
        ValidateHeaderAndLrc(frame, expectedStation, expectedCmdSubcmd, out _);
    }

    private static int ValidateHeaderAndLrc(byte[] frame, byte expectedStation, string expectedCmdSubcmd, out int dataCharCount)
    {
        if (frame is null || frame.Length < 17)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != (byte)':' || frame[frame.Length - 2] != 0x0D || frame[frame.Length - 1] != 0x0A)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 프레임 헤더(:) 또는 종료(CRLF)가 올바르지 않습니다.");
        }

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, 1, 2);
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        int declaredByteLength = BinaryBuffer.ReadAsciiHex(frame, 3, 2);
        byte endCodeVal = (byte)BinaryBuffer.ReadAsciiHex(frame, 9, 2);
        if (endCodeVal != 0x00)
        {
            string endCodeHex = Encoding.ASCII.GetString(frame, 9, 2).ToUpperInvariant();
            string message = GetErrorMessage(endCodeHex);
            throw new PlcDeviceException(endCodeVal, $"Fuji SPB 장치 오류 (코드 {endCodeHex}): {message}");
        }

        string cmdEcho = Encoding.ASCII.GetString(frame, 11, 4);
        if (!string.Equals(cmdEcho, expectedCmdSubcmd, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 명령 에코 불일치: 예상 {expectedCmdSubcmd}, 수신 {cmdEcho}");
        }

        // ':'(1) + Station(2) + Len(2) + Body(declaredByteLength * 2) + LRC(2) + CRLF(2)
        int expectedLengthWithLrc = 1 + 4 + (declaredByteLength * 2) + 2 + 2;
        if (frame.Length == expectedLengthWithLrc)
        {
            int lrcOffset = frame.Length - 4;
            int sum = 0;
            for (int i = 1; i < lrcOffset; i++)
            {
                sum += frame[i];
            }

            byte expectedLrc = (byte)((-sum) & 0xFF);
            byte actualLrc = (byte)BinaryBuffer.ReadAsciiHex(frame, lrcOffset, 2);
            if (expectedLrc != actualLrc)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB LRC 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {actualLrc:X2}");
            }

            dataCharCount = lrcOffset - 15;
        }
        else
        {
            dataCharCount = (frame.Length - 2) - 15;
        }

        return 15;
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
