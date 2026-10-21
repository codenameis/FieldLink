using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 응답 프레임을 검증하고 데이터를 추출하는 파서입니다. (사양서 §2)</summary>
public static class FatekResponseParser
{
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    /// <summary>연속 비트 읽기 응답을 검증하고 비트 바이트 배열(0 또는 1)을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedCount">기대하는 비트 수입니다.</param>
    /// <returns>추출된 비트 바이트 배열입니다.</returns>
    public static byte[] ParseReadBits(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, "44");
        if (bodyLength < expectedCount)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신된 비트 응답 길이가 부족합니다. (필요: {expectedCount}, 수신: {bodyLength})");
        }

        byte[] result = new byte[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            result[i] = frame[6 + i] switch
            {
                (byte)'1' => 1,
                (byte)'0' => 0,
                _ => throw new PlcException(PlcErrorKind.InvalidResponse, $"유효하지 않은 FATEK 비트 문자입니다: 0x{frame[6 + i]:X2}")
            };
        }

        return result;
    }

    /// <summary>연속 워드 읽기 응답을 검증하고 리틀 엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 워드 데이터 바이트 배열(리틀 엔디언)입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedStation, ushort expectedCount)
    {
        int bodyLength = ValidateHeaderAndChecksum(frame, expectedStation, "46");
        int requiredChars = expectedCount * 4;
        if (bodyLength < requiredChars)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"수신된 워드 응답 길이가 부족합니다. (필요: {requiredChars}, 수신: {bodyLength})");
        }

        byte[] result = new byte[expectedCount * 2];
        for (int i = 0; i < expectedCount; i++)
        {
            ushort value = (ushort)BinaryBuffer.ReadAsciiHex(frame, 6 + (i * 4), 4);
            BinaryBuffer.WriteUInt16LE(result, i * 2, value);
        }

        return result;
    }

    /// <summary>쓰기 명령(비트 또는 워드) 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 응답 프레임입니다.</param>
    /// <param name="expectedStation">기대하는 국번입니다.</param>
    /// <param name="expectedCommand">기대하는 2문자 명령 에코(예: "45", "47")입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedStation, string expectedCommand)
    {
        ValidateHeaderAndChecksum(frame, expectedStation, expectedCommand);
    }

    private static int ValidateHeaderAndChecksum(byte[] frame, byte expectedStation, string expectedCommand)
    {
        if (frame is null || frame.Length < 9)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 응답 프레임 길이가 너무 짧습니다.");
        }

        if (frame[0] != Stx || frame[frame.Length - 1] != Etx)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "FATEK 시작(STX) 또는 종료(ETX) 문자가 올바르지 않습니다.");
        }

        ValidateChecksum(frame);

        byte station = (byte)BinaryBuffer.ReadAsciiHex(frame, 1, 2);
        if (station != expectedStation)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"국번 불일치: 예상 {expectedStation:X2}, 수신 {station:X2}");
        }

        string command = Encoding.ASCII.GetString(frame, 3, 2);
        if (!string.Equals(command, expectedCommand, StringComparison.OrdinalIgnoreCase))
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"명령 에코 불일치: 예상 {expectedCommand}, 수신 {command}");
        }

        char status = (char)frame[5];
        if (status != '0')
        {
            string message = GetErrorMessage(status);
            throw new PlcDeviceException(status, $"FATEK 장치 오류 (코드 '{status}'): {message}");
        }

        return frame.Length - 9;
    }

    private static void ValidateChecksum(byte[] frame)
    {
        int sum = Stx;
        for (int i = 1; i < frame.Length - 3; i++)
        {
            sum += frame[i];
        }

        byte expectedLrc = (byte)(sum & 0xFF);
        byte actualLrc = (byte)BinaryBuffer.ReadAsciiHex(frame, frame.Length - 3, 2);
        if (expectedLrc != actualLrc)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"FATEK 체크섬 불일치: 계산 {expectedLrc:X2}, 수신 {actualLrc:X2}");
        }
    }

    private static string GetErrorMessage(char code) => code switch
    {
        '2' => "데이터 값 범위 초과 (Illegal value)",
        '3' => "포맷 오류 또는 쓰기 금지 / 명령 실행 불가 (Illegal format / write protected)",
        '4' => "정의되지 않은 명령 코드 (Invalid command code)",
        '5' => "RUN 전환 불가 — 래더 체크섬 불일치",
        '6' => "RUN 전환 불가 — PLC ID와 래더 ID 불일치",
        '7' => "RUN 전환 불가 — 래더 문법 오류",
        '9' => "RUN 전환 불가 — 미지원 명령어 포함",
        'A' => "컴포넌트 기호 또는 주소 범위 초과 (Illegal address)",
        _ => "알 수 없는 FATEK 오류",
    };
}
