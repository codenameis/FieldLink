using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part A §A.2)</summary>
public static class FujiElectricSpbResponseParser
{
    private const int MinimumFrameLength = 17;
    private const int HeaderPrefixHexLength = 5; // ':' + Station(2) + Len(2)
    private const int StationOffset = 1;
    private const int LengthFieldOffset = 3;
    private const int EndCodeOffset = 9;
    private const int CommandEchoOffset = 11;
    private const int CommandEchoLength = 4;
    private const int DataStartOffset = 15;
    private const int AsciiCharsPerByte = 2;
    private const int HexCharsPerWord = 4;
    private const int BytesPerWord = 2;
    private const int LrcAndTrailerLength = 4;
    private const int TrailerLength = 2;
    private const byte AsciiColon = (byte)':';
    private const byte AsciiCarriageReturn = 0x0D;
    private const byte AsciiLineFeed = 0x0A;
    private const byte SuccessEndCode = 0x00;
    private const int ByteMask = 0xFF;
    private const string CommandReadWords = "0000";

    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        int dataOffset = ValidateHeaderAndLrc(frame, expectedStation, CommandReadWords, out int availableDataChars);
        int expectedChars = expectedCount * HexCharsPerWord;

        if (availableDataChars < expectedChars)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신 데이터 길이 부족: 필요 {expectedChars}, 수신 {availableDataChars}");
        }

        byte[] result = new byte[expectedCount * BytesPerWord];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)BinaryBuffer.ReadAsciiHex(frame, dataOffset + (i * AsciiCharsPerByte), AsciiCharsPerByte);
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
        if (frame is null || frame.Length < MinimumFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != AsciiColon || frame[frame.Length - TrailerLength] != AsciiCarriageReturn || frame[frame.Length - 1] != AsciiLineFeed)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 프레임 헤더(:) 또는 종료(CRLF)가 올바르지 않습니다.");
        }

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, StationOffset, AsciiCharsPerByte);
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        int declaredByteLength = BinaryBuffer.ReadAsciiHex(frame, LengthFieldOffset, AsciiCharsPerByte);
        byte endCodeVal = (byte)BinaryBuffer.ReadAsciiHex(frame, EndCodeOffset, AsciiCharsPerByte);
        if (endCodeVal != SuccessEndCode)
        {
            string endCodeHex = Encoding.ASCII.GetString(frame, EndCodeOffset, AsciiCharsPerByte).ToUpperInvariant();
            string message = GetErrorMessage(endCodeHex);
            throw new PlcDeviceException(endCodeVal, $"Fuji SPB 장치 오류 (코드 {endCodeHex}): {message}");
        }

        string cmdEcho = Encoding.ASCII.GetString(frame, CommandEchoOffset, CommandEchoLength);
        if (!string.Equals(cmdEcho, expectedCmdSubcmd, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB 명령 에코 불일치: 예상 {expectedCmdSubcmd}, 수신 {cmdEcho}");
        }

        // ':'(1) + Station(2) + Len(2) + Body(declaredByteLength * 2) + LRC(2) + CRLF(2)
        int expectedLengthWithLrc = HeaderPrefixHexLength + (declaredByteLength * AsciiCharsPerByte) + LrcAndTrailerLength;
        if (frame.Length == expectedLengthWithLrc)
        {
            int lrcOffset = frame.Length - LrcAndTrailerLength;
            int sum = 0;
            for (int i = 1; i < lrcOffset; i++)
            {
                sum += frame[i];
            }

            byte expectedLrc = (byte)((-sum) & ByteMask);
            byte actualLrc = (byte)BinaryBuffer.ReadAsciiHex(frame, lrcOffset, AsciiCharsPerByte);
            if (expectedLrc != actualLrc)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"Fuji SPB LRC 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {actualLrc:X2}");
            }

            dataCharCount = lrcOffset - DataStartOffset;
        }
        else
        {
            dataCharCount = (frame.Length - TrailerLength) - DataStartOffset;
        }

        return DataStartOffset;
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
