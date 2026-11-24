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
    private const byte MinStation = 1;
    private const byte MaxStation = 255;
    private const string DirectCpuExtendedStation = "FFFF";

    private const int StationOffset = 1;
    private const int StationDigitCount = 2;
    private const int DeclaredLengthOffset = 3;
    private const int DeclaredLengthDigitCount = 2;
    private const int ExtendedStationOffset = 5;
    private const int ExtendedStationDigitCount = 4;
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
    /// <param name="expectedStation">기대하는 PLC 국번(1~255)입니다.</param>
    /// <param name="expectedCount">요청한 워드 개수입니다.</param>
    /// <param name="expectLrc">응답 프레임에 2문자 ASCII LRC가 반드시 포함되어야 하는지 여부입니다.</param>
    /// <returns>추출된 리틀엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(
        byte[] frame,
        byte expectedStation,
        ushort expectedCount,
        bool expectLrc = false)
    {
        if (expectedCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), expectedCount, "기대 워드 수는 1 이상이어야 합니다.");
        }

        int expectedDataBytes = expectedCount * BytesPerWord;
        ValidateResponse(frame, expectedStation, ReadWordsCommandEcho, expectedDataBytes, expectLrc);

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
    /// <param name="expectedStation">기대하는 PLC 국번(1~255)입니다.</param>
    /// <param name="expectedCommandEcho">기대하는 4문자 명령+서브명령 에코입니다.</param>
    /// <param name="expectLrc">응답 프레임에 2문자 ASCII LRC가 반드시 포함되어야 하는지 여부입니다.</param>
    public static void VerifyWriteResponse(
        byte[] frame,
        byte expectedStation,
        string expectedCommandEcho,
        bool expectLrc = false)
    {
        if (string.IsNullOrEmpty(expectedCommandEcho) || expectedCommandEcho.Length != CommandEchoDigitCount)
        {
            throw new ArgumentException($"기대 명령 에코는 {CommandEchoDigitCount}문자여야 합니다.", nameof(expectedCommandEcho));
        }

        ValidateResponse(frame, expectedStation, expectedCommandEcho, expectedDataByteCount: 0, expectLrc);
    }

    private static void ValidateResponse(
        byte[] frame,
        byte expectedStation,
        string expectedCommandEcho,
        int expectedDataByteCount,
        bool expectLrc)
    {
        ValidateEnvelope(frame, expectedStation, expectLrc);
        int actualBodyByteLength = ValidateDeclaredLengthAndLrc(frame, expectLrc);
        ValidateCommandEcho(frame, expectedCommandEcho);
        ValidateEndCodeAndBodySize(frame, actualBodyByteLength, expectedDataByteCount);
    }

    private static void ValidateEnvelope(byte[] frame, byte expectedStation, bool expectLrc)
    {
        if (expectedStation is < MinStation or > MaxStation)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedStation), expectedStation, $"Fuji SPB 국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
        }

        int minimumFrameLength = expectLrc ? MinimumFrameLengthWithLrc : MinimumFrameLengthWithoutLrc;
        if (frame is null || frame.Length < minimumFrameLength)
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

        string extendedStation = Encoding.ASCII.GetString(frame, ExtendedStationOffset, ExtendedStationDigitCount);
        if (!string.Equals(extendedStation, DirectCpuExtendedStation, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 확장 국번 불일치: 예상 {DirectCpuExtendedStation}, 수신 {extendedStation}");
        }
    }

    private static int ValidateDeclaredLengthAndLrc(byte[] frame, bool expectLrc)
    {
        int lrcLength = expectLrc ? LrcDigitCount : 0;
        int bodyAsciiLength = frame.Length - HeaderDigitCount - lrcLength - TrailerLength;
        if (bodyAsciiLength % DigitsPerByte != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 응답 프레임의 ASCII 본문 길이({bodyAsciiLength})가 짝수가 아닙니다.");
        }

        int actualBodyByteLength = bodyAsciiLength / DigitsPerByte;
        int declaredByteLength = BinaryBuffer.ReadAsciiHex(frame, DeclaredLengthOffset, DeclaredLengthDigitCount);
        if (declaredByteLength != actualBodyByteLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 본문 바이트 길이 불일치: 헤더 선언 {declaredByteLength}바이트, 실제 본문 {actualBodyByteLength}바이트");
        }

        if (expectLrc)
        {
            int lrcOffset = frame.Length - TrailerLength - LrcDigitCount;
            VerifyLrcChecksum(frame, lrcOffset);
        }

        return actualBodyByteLength;
    }

    private static void ValidateCommandEcho(byte[] frame, string expectedCommandEcho)
    {
        string commandEcho = Encoding.ASCII.GetString(frame, CommandEchoOffset, CommandEchoDigitCount);
        if (!string.Equals(commandEcho, expectedCommandEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 명령 에코 불일치: 예상 {expectedCommandEcho}, 수신 {commandEcho}");
        }
    }

    private static void ValidateEndCodeAndBodySize(
        byte[] frame,
        int actualBodyByteLength,
        int expectedDataByteCount)
    {
        byte endCodeValue = (byte)BinaryBuffer.ReadAsciiHex(frame, EndCodeOffset, EndCodeDigitCount);
        var errorCode = (FujiElectricSpbErrorCode)endCodeValue;
        if (errorCode != FujiElectricSpbErrorCode.Normal)
        {
            if (actualBodyByteLength != FixedBodyByteLength)
            {
                throw new PlcException(
                    PlcErrorKind.InvalidResponse,
                    $"Fuji SPB 오류 응답 본문 길이가 {FixedBodyByteLength}바이트가 아닙니다. (실제: {actualBodyByteLength})");
            }

            throw new PlcDeviceException(
                endCodeValue,
                $"Fuji SPB 장치 오류 (코드 {endCodeValue:X2}): {errorCode.GetDescription()}");
        }

        int expectedBodyByteLength = FixedBodyByteLength + expectedDataByteCount;
        if (actualBodyByteLength != expectedBodyByteLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"Fuji SPB 정상 응답 본문 길이가 기대값({expectedBodyByteLength}바이트)과 일치하지 않습니다. (실제: {actualBodyByteLength}바이트)");
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
}
