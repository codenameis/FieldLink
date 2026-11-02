using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part A §A.2)</summary>
public static class FujiElectricSpbResponseParser
{
    internal const string ReadWordsCommandEcho = "0000";
    internal const string WriteWordsCommandEcho = "0100";
    internal const string WriteBitCommandEcho = "0102";

    private const byte StartColon = (byte)':';
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const byte NormalEndCode = 0x00;

    private const int StationOffset = 1;
    private const int StationDigitCount = 2;
    private const int DeclaredLengthOffset = 3;
    private const int DeclaredLengthDigitCount = 2;
    private const int HeaderDigitCount = 5;
    private const int EndCodeOffset = 9;
    private const int EndCodeDigitCount = 2;
    private const int CommandEchoOffset = 11;
    private const int CommandEchoDigitCount = 4;
    private const int WordDataOffset = 15;
    private const int FixedBodyByteLength = 5;

    private const int DigitsPerByte = 2;
    private const int BytesPerWord = 2;
    private const int LrcDigitCount = 2;
    private const int TrailerLength = 2;
    private const int MinimumFrameLengthWithoutLrc = 17;
    private const int MinimumFrameLengthWithLrc = 19;
    private const int LowByteMask = 0xFF;

    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 ASCII 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 PLC 국번입니다.</param>
    /// <param name="expectedCount">요청한 워드 개수입니다.</param>
    /// <returns>추출된 리틀엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        int expectedDataBytes = expectedCount * BytesPerWord;
        ValidateResponse(frame, expectedStation, ReadWordsCommandEcho, expectedDataBytes);

        byte[] result = new byte[expectedDataBytes];
        for (int i = 0; i < result.Length; i++)
        {
            int hexOffset = WordDataOffset + (i * DigitsPerByte);
            result[i] = (byte)BinaryBuffer.ReadAsciiHex(frame, hexOffset, DigitsPerByte);
        }

        return result;
    }

    /// <summary>쓰기 명령(워드 또는 단일 비트) 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 ASCII 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 PLC 국번입니다.</param>
    /// <param name="expectedCommandEcho">기대하는 4문자 명령+서브명령 에코입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCommandEcho)
    {
        ValidateResponse(frame, expectedStation, expectedCommandEcho, expectedDataByteCount: 0);
    }

    private static void ValidateResponse(
        byte[] frame,
        byte expectedStation,
        string expectedCommandEcho,
        int expectedDataByteCount)
    {
        ValidateEnvelope(frame, expectedStation);
        ValidateStatusAndCommandEcho(frame, expectedCommandEcho);
        ValidateLengthAndLrc(frame, expectedDataByteCount);
    }

    private static void ValidateEnvelope(byte[] frame, byte expectedStation)
    {
        if (frame is null || frame.Length < MinimumFrameLengthWithoutLrc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 응답 프레임 길이가 너무 짧습니다.");
        }

        bool hasValidDelimiters = frame[0] == StartColon
            && frame[frame.Length - TrailerLength] == CarriageReturn
            && frame[frame.Length - 1] == LineFeed;
        if (!hasValidDelimiters)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "Fuji SPB 프레임 시작(:) 또는 종료(CRLF) 구분자가 올바르지 않습니다.");
        }

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, StationOffset, StationDigitCount);
        if (station != expectedStation)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }
    }

    private static void ValidateStatusAndCommandEcho(byte[] frame, string expectedCommandEcho)
    {
        byte endCode = (byte)BinaryBuffer.ReadAsciiHex(frame, EndCodeOffset, EndCodeDigitCount);
        if (endCode != NormalEndCode)
        {
            string endCodeHex = Encoding.ASCII.GetString(frame, EndCodeOffset, EndCodeDigitCount).ToUpperInvariant();
            string errorMessage = GetErrorMessage(endCodeHex);
            throw new PlcDeviceException(endCode, $"Fuji SPB 장치 오류 (코드 {endCodeHex}): {errorMessage}");
        }

        string commandEcho = Encoding.ASCII.GetString(frame, CommandEchoOffset, CommandEchoDigitCount);
        if (!string.Equals(commandEcho, expectedCommandEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 명령 에코 불일치: 예상 {expectedCommandEcho}, 수신 {commandEcho}");
        }
    }

    private static void ValidateLengthAndLrc(byte[] frame, int expectedDataByteCount)
    {
        int declaredByteLength = BinaryBuffer.ReadAsciiHex(frame, DeclaredLengthOffset, DeclaredLengthDigitCount);
        int expectedBodyByteLength = FixedBodyByteLength + expectedDataByteCount;
        int expectedLengthWithoutLrc = MinimumFrameLengthWithoutLrc + (expectedDataByteCount * DigitsPerByte);
        int expectedLengthWithLrc = MinimumFrameLengthWithLrc + (expectedDataByteCount * DigitsPerByte);

        if (frame.Length == expectedLengthWithoutLrc)
        {
            int actualBodyByteLength = (frame.Length - HeaderDigitCount - TrailerLength) / DigitsPerByte;
            EnsureDeclaredLengthMatches(declaredByteLength, actualBodyByteLength);
            return;
        }

        if (frame.Length == expectedLengthWithLrc)
        {
            int actualBodyByteLength = (frame.Length - HeaderDigitCount - LrcDigitCount - TrailerLength) / DigitsPerByte;
            EnsureDeclaredLengthMatches(declaredByteLength, actualBodyByteLength);
            int lrcOffset = frame.Length - TrailerLength - LrcDigitCount;
            VerifyLrcChecksum(frame, lrcOffset);
            return;
        }

        throw new PlcException(
            PlcErrorKind.InvalidResponse,
            $"Fuji SPB 응답 프레임 길이가 올바르지 않습니다: 선언 본문 {declaredByteLength}바이트, 기대 본문 {expectedBodyByteLength}바이트, 프레임 길이 {frame.Length}");
    }

    private static void EnsureDeclaredLengthMatches(int declaredByteLength, int actualBodyByteLength)
    {
        if (declaredByteLength != actualBodyByteLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 본문 바이트 길이 불일치: 헤더 선언 {declaredByteLength}바이트, 실제 본문 {actualBodyByteLength}바이트");
        }
    }

    private static void VerifyLrcChecksum(byte[] frame, int lrcOffset)
    {
        int asciiByteSum = 0;
        for (int i = StationOffset; i < lrcOffset; i++)
        {
            asciiByteSum += frame[i];
        }

        byte expectedLrc = (byte)((-asciiByteSum) & LowByteMask);
        byte actualLrc = (byte)BinaryBuffer.ReadAsciiHex(frame, lrcOffset, LrcDigitCount);
        if (expectedLrc != actualLrc)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB LRC 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {actualLrc:X2}");
        }
    }

    private static string GetErrorMessage(string endCodeHex) => endCodeHex switch
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
