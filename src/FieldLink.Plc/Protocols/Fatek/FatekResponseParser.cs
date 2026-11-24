using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 응답 프레임을 검증하고 데이터를 추출하는 파서입니다. (사양서 §2)</summary>
public static class FatekResponseParser
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    private const byte MinStation = 1;
    private const byte MaxStation = 254;
    private const int MinimumFrameLength = 9;
    private const int StationOffset = 1;
    private const int StationHexLength = 2;
    private const int CommandOffset = 3;
    private const int CommandLength = 2;
    private const int StatusCharOffset = 5;
    private const int DataStartOffset = 6;
    private const int TrailerLength = 3;
    private const int ChecksumHexLength = 2;
    private const int HexCharsPerByte = 2;
    private const int HexCharsPerWord = 4;
    private const int BytesPerWord = 2;
    private const int HexCharsPerDWord = 8;
    private const int BytesPerDWord = 4;
    private const int StatusByteCount = 3;
    private const int StatusHexLength = StatusByteCount * HexCharsPerByte;
    private const int BitsPerWord = 16;
    private const int ChecksumByteMask = 0xFF;

    private const byte BitOnAscii = (byte)'1';
    private const byte BitOffAscii = (byte)'0';
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;
    private const string CommandReadStatus = "40";
    private const string CommandReadBits = "44";
    private const string CommandReadWords = "46";

    /// <summary>PLC 시스템 상태 요약(명령 40) 응답을 검증하고 3바이트 상태 배열(Status1~Status3)을 추출합니다. (사양서 §2.3)</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~254)입니다.</param>
    /// <returns>추출된 3바이트 상태 배열입니다.</returns>
    public static byte[] ParseReadStatus(byte[] frame, byte expectedStation)
    {
        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, CommandReadStatus);
        if (bodyLength != StatusHexLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신된 상태 읽기 응답 길이가 일치하지 않습니다. (필요: {StatusHexLength}, 수신: {bodyLength})");
        }

        byte[] statusBytes = new byte[StatusByteCount];
        for (int i = 0; i < StatusByteCount; i++)
        {
            int charOffset = DataStartOffset + (i * HexCharsPerByte);
            statusBytes[i] = (byte)BinaryBuffer.ReadAsciiHex(frame, charOffset, HexCharsPerByte);
        }

        return statusBytes;
    }

    /// <summary>연속 비트 읽기 응답을 검증하고 비트 바이트 배열(0 또는 1)을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~254)입니다.</param>
    /// <param name="expectedCount">기대하는 비트 수입니다.</param>
    /// <returns>추출된 비트 바이트 배열입니다.</returns>
    public static byte[] ParseReadBits(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        if (expectedCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), expectedCount, "기대 비트 수는 1 이상이어야 합니다.");
        }

        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, CommandReadBits);
        if (bodyLength != expectedCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신된 비트 응답 길이가 일치하지 않습니다. (필요: {expectedCount}, 수신: {bodyLength})");
        }

        byte[] result = new byte[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            byte bitChar = frame[DataStartOffset + i];
            result[i] = bitChar switch
            {
                BitOnAscii => BitOnValue,
                BitOffAscii => BitOffValue,
                _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 FATEK 비트 문자입니다: 0x{bitChar:X2}"),
            };
        }

        return result;
    }

    /// <summary>연속 워드 읽기 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~254)입니다.</param>
    /// <param name="expectedCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        if (expectedCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), expectedCount, "기대 워드 수는 1 이상이어야 합니다.");
        }

        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, CommandReadWords);
        int requiredCharCount = expectedCount * HexCharsPerWord;
        if (bodyLength != requiredCharCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신된 워드 응답 길이가 일치하지 않습니다. (필요: {requiredCharCount}, 수신: {bodyLength})");
        }

        byte[] result = new byte[expectedCount * BytesPerWord];
        for (int i = 0; i < expectedCount; i++)
        {
            int charOffset = DataStartOffset + (i * HexCharsPerWord);
            ushort wordValue = (ushort)BinaryBuffer.ReadAsciiHex(frame, charOffset, HexCharsPerWord);
            BinaryBuffer.WriteUInt16LittleEndian(result, i * BytesPerWord, wordValue);
        }

        return result;
    }

    /// <summary>연속 32비트 레지스터(DR/DD/DF/DRT/DRC/DW*) 읽기 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~254)입니다.</param>
    /// <param name="expectedCount">기대하는 32비트 레지스터 수입니다.</param>
    /// <returns>추출된 32비트 레지스터 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadDWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        if (expectedCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), expectedCount, "기대 32비트 레지스터 수는 1 이상이어야 합니다.");
        }

        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, CommandReadWords);
        int requiredCharCount = expectedCount * HexCharsPerDWord;
        if (bodyLength != requiredCharCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"수신된 32비트 레지스터 응답 길이가 일치하지 않습니다. (필요: {requiredCharCount}, 수신: {bodyLength})");
        }

        byte[] result = new byte[expectedCount * BytesPerDWord];
        for (int i = 0; i < expectedCount; i++)
        {
            int charOffset = DataStartOffset + (i * HexCharsPerDWord);
            ushort highWord = (ushort)BinaryBuffer.ReadAsciiHex(frame, charOffset, HexCharsPerWord);
            ushort lowWord = (ushort)BinaryBuffer.ReadAsciiHex(frame, charOffset + HexCharsPerWord, HexCharsPerWord);
            uint dwordValue = ((uint)highWord << BitsPerWord) | lowWord;
            BinaryBuffer.WriteUInt32LittleEndian(result, i * BytesPerDWord, dwordValue);
        }

        return result;
    }

    /// <summary>쓰기 명령(비트 또는 워드) 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번(1~254)입니다.</param>
    /// <param name="expectedCommand">기대하는 2문자 명령 에코(예: "45", "47")입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCommand)
    {
        if (string.IsNullOrEmpty(expectedCommand))
        {
            throw new ArgumentException("기대 명령 코드는 비어 있을 수 없습니다.", nameof(expectedCommand));
        }

        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, expectedCommand);
        if (bodyLength != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"FATEK 쓰기 응답 본문 길이가 0이 아닙니다. (실제: {bodyLength})");
        }
    }

    private static int ValidateHeaderAndChecksum(byte[] frame, byte expectedStation, string expectedCommand)
    {
        if (expectedStation is < MinStation or > MaxStation)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedStation), expectedStation, $"FATEK 국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
        }

        if (frame is null || frame.Length < MinimumFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != Stx || frame[frame.Length - 1] != Etx)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 시작(STX) 또는 종료(ETX) 문자가 올바르지 않습니다.");
        }

        ValidateChecksum(frame);

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, StationOffset, StationHexLength);
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        string command = Encoding.ASCII.GetString(frame, CommandOffset, CommandLength);
        if (!string.Equals(command, expectedCommand, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"명령 에코 불일치: 예상 {expectedCommand}, 수신 {command}");
        }

        char statusChar = (char)frame[StatusCharOffset];
        var errorCode = (FatekErrorCode)statusChar;
        if (errorCode != FatekErrorCode.ErrorFree)
        {
            throw new PlcDeviceException(statusChar, $"FATEK 장치 오류 (코드 '{statusChar}'): {errorCode.GetDescription()}");
        }

        return frame.Length - MinimumFrameLength;
    }

    private static void ValidateChecksum(byte[] frame)
    {
        int sum = Stx;
        for (int i = 1; i < frame.Length - TrailerLength; i++)
        {
            sum += frame[i];
        }

        byte expectedLrc = (byte)(sum & ChecksumByteMask);
        byte actualLrc = (byte)BinaryBuffer.ReadAsciiHex(frame, frame.Length - TrailerLength, ChecksumHexLength);
        if (expectedLrc != actualLrc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FATEK 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {actualLrc:X2}");
        }
    }
}
