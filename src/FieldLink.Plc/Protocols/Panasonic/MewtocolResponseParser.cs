using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class MewtocolResponseParser
{
    private const int MinimumFrameLength = 9;
    private const int BytesPerWord = 2;
    private const int HexCharsPerByte = 2;
    private const int HexCharsPerWord = BytesPerWord * HexCharsPerByte;
    private const int StationOffset = 1;
    private const int StatusOffset = 3;
    private const int CommandEchoOffset = 4;
    private const int CommandEchoLength = 2;
    private const int ErrorCodeLength = 2;
    private const int DataOffset = 6;
    private const int BccHexLength = 2;
    private const int TrailerLength = 3;
    private const int SingleBitDataLength = 1;
    private const int InvalidErrorNumber = -1;
    private const byte StandardHeader = (byte)'%';
    private const byte ExpandedHeader = (byte)'<';
    private const byte TerminatorCr = 0x0D;
    private const char ErrorStatusMarker = '!';
    private const char NormalStatusMarker = '$';
    private const byte BitOnAscii = (byte)'1';
    private const byte BitOffAscii = (byte)'0';
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;
    private const string CommandReadSingleBit = "RC";
    private const string CommandReadWords = "RD";

    /// <summary>단일 비트 읽기(RC) 응답을 검증하고 비트 값(0 또는 1)을 반환합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <returns>읽어온 비트 값(0 또는 1)입니다.</returns>
    public static byte ParseReadSingleBit(byte[] frame, byte expectedStation)
    {
        int dataLen = ValidateHeaderAndBcc(frame, expectedStation, CommandReadSingleBit);
        if (dataLen != SingleBitDataLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"비트 응답 데이터 길이가 올바르지 않습니다: {dataLen}");
        }

        return frame[DataOffset] switch
        {
            BitOnAscii => BitOnValue,
            BitOffAscii => BitOffValue,
            _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 비트 응답 문자입니다: 0x{frame[DataOffset]:X2}")
        };
    }

    /// <summary>연속 워드 읽기(RD) 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedWordCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedWordCount)
    {
        if (expectedWordCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWordCount), expectedWordCount, "기대 워드 수는 1 이상이어야 합니다.");
        }

        int dataLen = ValidateHeaderAndBcc(frame, expectedStation, CommandReadWords);
        int expectedChars = expectedWordCount * HexCharsPerWord;
        if (dataLen != expectedChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신 데이터 길이 불일치: 필요 {expectedChars}, 수신 {dataLen}");
        }

        byte[] result = new byte[expectedWordCount * BytesPerWord];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ParseHexByte(frame, DataOffset + (i * HexCharsPerByte), "워드 데이터");
        }

        return result;
    }

    /// <summary>쓰기 명령 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedEcho">기대하는 2글자 명령 에코(예: "WC", "WD")입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedEcho)
    {
        if (string.IsNullOrEmpty(expectedEcho))
        {
            throw new ArgumentException("기대 명령 에코 문자열은 필수입니다.", nameof(expectedEcho));
        }

        ValidateHeaderAndBcc(frame, expectedStation, expectedEcho);
    }

    private static int ValidateHeaderAndBcc(byte[] frame, byte expectedStation, string expectedEcho)
    {
        if (frame is null || frame.Length < MinimumFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != StandardHeader && frame[0] != ExpandedHeader)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 시작 헤더('%' 또는 '<')가 올바르지 않습니다: 0x{frame[0]:X2}");
        }

        if (frame[frame.Length - 1] != TerminatorCr)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 종료자(CR)가 없습니다.");
        }

        ValidateBcc(frame);

        byte station = ParseHexByte(frame, StationOffset, "국번");
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        char status = (char)frame[StatusOffset];
        if (status == ErrorStatusMarker)
        {
            string errCode = Encoding.ASCII.GetString(frame, CommandEchoOffset, ErrorCodeLength);
            int codeVal = int.TryParse(errCode, NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                ? n
                : InvalidErrorNumber;
            string message = GetErrorMessage(errCode);
            throw new PlcDeviceException(codeVal, $"MEWTOCOL 에러 (코드 {errCode}): {message}");
        }

        if (status != NormalStatusMarker)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"알 수 없는 MEWTOCOL 상태 마커: {status}");
        }

        string cmdEcho = Encoding.ASCII.GetString(frame, CommandEchoOffset, CommandEchoLength);
        if (!string.Equals(cmdEcho, expectedEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 명령 에코 불일치: 예상 {expectedEcho}, 수신 {cmdEcho}");
        }

        return (frame.Length - TrailerLength) - DataOffset;
    }

    private static void ValidateBcc(byte[] frame)
    {
        byte calculatedBcc = 0;
        for (int i = 0; i < frame.Length - TrailerLength; i++)
        {
            calculatedBcc ^= frame[i];
        }

        byte actualBcc = ParseHexByte(frame, frame.Length - TrailerLength, "BCC");
        if (calculatedBcc != actualBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL BCC 불일치: 계산 {calculatedBcc:X2}, 수신 {actualBcc:X2}");
        }
    }

    private static byte ParseHexByte(byte[] frame, int offset, string fieldName)
    {
        string text = Encoding.ASCII.GetString(frame, offset, BccHexLength);
        if (!byte.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL {fieldName} 16진수 파싱 실패: '{text}'");
        }

        return value;
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
