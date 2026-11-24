using System;
using System.Globalization;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class MewtocolResponseParser
{
    private const byte StandardHeaderByte = (byte)'%';
    private const byte ExpandedHeaderByte = (byte)'<';
    private const byte CarriageReturnByte = 0x0D;
    private const byte BitOnCharByte = (byte)'1';
    private const byte BitOffCharByte = (byte)'0';
    private const byte MinStationNumber = 1;
    private const byte MaxStationNumber = 99;
    private const byte DirectConnectionStation = 0xEE;
    private const string DirectConnectionStationText = "EE";
    private const char SuccessStatusMarker = '$';
    private const char ErrorStatusMarker = '!';

    private const int MinimumFrameLength = 9;
    private const int StationOffset = 1;
    private const int StationLength = 2;
    private const int StatusMarkerOffset = 3;
    private const int CommandOrErrorCodeOffset = 4;
    private const int CommandOrErrorCodeLength = 2;
    private const int DataStartOffset = 6;
    private const int TrailerLength = 3; // BCC(2) + CR(1)
    private const int BccLength = 2;
    private const int SingleBitCharacterCount = 1;
    private const int HexCharsPerByte = 2;
    private const int HexCharsPerWord = 4;
    private const int BytesPerWord = 2;

    /// <summary>단일 비트 읽기(RC) 응답을 검증하고 비트 값(0 또는 1)을 반환합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~99 또는 0xEE)입니다.</param>
    /// <returns>읽어온 비트 값(0 또는 1)입니다.</returns>
    public static byte ParseReadSingleBit(byte[] responseFrame, byte expectedStation)
    {
        int dataLength = ValidateHeaderAndBcc(responseFrame, expectedStation, "RC");
        if (dataLength != SingleBitCharacterCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"비트 응답 데이터 길이가 올바르지 않습니다: 기대 {SingleBitCharacterCount}, 수신 {dataLength}");
        }

        return responseFrame[DataStartOffset] switch
        {
            BitOnCharByte => 1,
            BitOffCharByte => 0,
            _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 비트 응답 문자입니다: 0x{responseFrame[DataStartOffset]:X2}")
        };
    }

    /// <summary>연속 워드 읽기(RD) 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="expectedWordCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadWords(byte[] responseFrame, byte expectedStation, ushort expectedWordCount)
    {
        if (expectedWordCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWordCount), expectedWordCount, "기대 워드 수는 1 이상이어야 합니다.");
        }

        int dataLength = ValidateHeaderAndBcc(responseFrame, expectedStation, "RD");
        int expectedCharacterCount = expectedWordCount * HexCharsPerWord;
        if (dataLength != expectedCharacterCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신 데이터 길이 불일치: 기대 {expectedCharacterCount}, 수신 {dataLength}");
        }

        byte[] wordBytes = new byte[expectedWordCount * BytesPerWord];
        for (int i = 0; i < wordBytes.Length; i++)
        {
            int charOffset = DataStartOffset + (i * HexCharsPerByte);
            wordBytes[i] = (byte)BinaryBuffer.ReadAsciiHex(responseFrame, charOffset, HexCharsPerByte);
        }

        return wordBytes;
    }

    /// <summary>쓰기 명령 응답을 검증합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~99 또는 0xEE)입니다.</param>
    /// <param name="expectedEcho">기대하는 2글자 명령 에코(예: "WC", "WD")입니다.</param>
    public static void VerifyWriteResponse(byte[] responseFrame, byte expectedStation, string expectedEcho)
    {
        if (string.IsNullOrEmpty(expectedEcho))
        {
            throw new ArgumentException("기대 명령 에코 문자열은 필수입니다.", nameof(expectedEcho));
        }

        int dataLength = ValidateHeaderAndBcc(responseFrame, expectedStation, expectedEcho);
        if (dataLength != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"쓰기 응답에 예상치 못한 데이터({dataLength}바이트)가 포함되었습니다.");
        }
    }

    private static int ValidateHeaderAndBcc(byte[] responseFrame, byte expectedStation, string expectedEcho)
    {
        string expectedStationText = MewtocolRequestBuilder.FormatStation(expectedStation);
        ValidateFrameStructure(responseFrame);
        ValidateBcc(responseFrame);
        ValidateStation(responseFrame, expectedStation, expectedStationText);
        ValidateStatusAndEcho(responseFrame, expectedEcho);

        return (responseFrame.Length - TrailerLength) - DataStartOffset;
    }

    private static void ValidateFrameStructure(byte[] responseFrame)
    {
        if (responseFrame is null || responseFrame.Length < MinimumFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 프레임 길이가 너무 짧습니다.");
        }

        if (responseFrame[0] != StandardHeaderByte && responseFrame[0] != ExpandedHeaderByte)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 시작 헤더('%' 또는 '<')가 올바르지 않습니다: 0x{responseFrame[0]:X2}");
        }

        if (responseFrame[responseFrame.Length - 1] != CarriageReturnByte)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "MEWTOCOL 응답 종료자(CR)가 없습니다.");
        }
    }

    private static void ValidateStation(byte[] responseFrame, byte expectedStation, string expectedStationText)
    {
        string stationText = Encoding.ASCII.GetString(responseFrame, StationOffset, StationLength);
        byte actualStation;

        if (string.Equals(stationText, DirectConnectionStationText, StringComparison.Ordinal))
        {
            actualStation = DirectConnectionStation;
        }
        else if (int.TryParse(stationText, NumberStyles.None, CultureInfo.InvariantCulture, out int decimalStation)
            && decimalStation >= MinStationNumber
            && decimalStation <= MaxStationNumber)
        {
            actualStation = (byte)decimalStation;
        }
        else
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 MEWTOCOL 국번 응답입니다: {stationText}");
        }

        if (actualStation != expectedStation)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"MEWTOCOL 국번 불일치: 예상 {expectedStationText}, 수신 {stationText}");
        }
    }

    private static void ValidateStatusAndEcho(byte[] responseFrame, string expectedEcho)
    {
        char statusMarker = (char)responseFrame[StatusMarkerOffset];
        if (statusMarker == ErrorStatusMarker)
        {
            string errorCodeText = Encoding.ASCII.GetString(responseFrame, CommandOrErrorCodeOffset, CommandOrErrorCodeLength);
            if (!int.TryParse(errorCodeText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedErrorCode))
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 MEWTOCOL 오류 코드입니다: {errorCodeText}");
            }

            string errorMessage = ((MewtocolErrorCode)parsedErrorCode).GetDescription();
            throw new PlcDeviceException(parsedErrorCode, $"MEWTOCOL 에러 (코드 {errorCodeText}): {errorMessage}");
        }

        if (statusMarker != SuccessStatusMarker)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"알 수 없는 MEWTOCOL 상태 마커: {statusMarker}");
        }

        string commandEcho = Encoding.ASCII.GetString(responseFrame, CommandOrErrorCodeOffset, CommandOrErrorCodeLength);
        if (!string.Equals(commandEcho, expectedEcho, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL 명령 에코 불일치: 예상 {expectedEcho}, 수신 {commandEcho}");
        }
    }

    private static void ValidateBcc(byte[] responseFrame)
    {
        int bccOffset = responseFrame.Length - TrailerLength;
        byte calculatedBcc = 0;
        for (int i = 0; i < bccOffset; i++)
        {
            calculatedBcc ^= responseFrame[i];
        }

        byte actualBcc = (byte)BinaryBuffer.ReadAsciiHex(responseFrame, bccOffset, BccLength);
        if (calculatedBcc != actualBcc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"MEWTOCOL BCC 불일치: 계산 {calculatedBcc:X2}, 수신 {actualBcc:X2}");
        }
    }
}
