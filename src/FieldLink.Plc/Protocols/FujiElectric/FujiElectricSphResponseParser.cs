using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part C §C.2)</summary>
public static class FujiElectricSphResponseParser
{
    private const byte FrameStartIdentifier = 0xFB;
    private const byte NormalEndCode = 0x00;
    private const byte ReadCommandCode = 0x00;
    private const byte WriteCommandCode = 0x01;

    private const int HeaderByteLength = 20;
    private const int EndCodeOffset = 4;
    private const int ConnectionIdOffset = 6;
    private const int CommandEchoOffset = 14;
    private const int PayloadLengthOffset = 18;
    private const int WordDataOffset = 26;
    private const int BytesPerWord = 2;

    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 SPH 응답 프레임입니다.</param>
    /// <param name="expectedConnectionId">기대하는 SX-Bus 연결 스테이션 ID입니다.</param>
    /// <param name="expectedWordCount">요청한 워드 개수입니다.</param>
    /// <returns>추출된 리틀엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedConnectionId, ushort expectedWordCount)
    {
        ValidateResponseHeader(frame, expectedConnectionId, ReadCommandCode);

        int expectedDataByteCount = expectedWordCount * BytesPerWord;
        int requiredFrameLength = WordDataOffset + expectedDataByteCount;
        if (frame.Length < requiredFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 데이터 길이가 부족합니다: 필요 {requiredFrameLength}, 수신 {frame.Length}");
        }

        byte[] result = new byte[expectedDataByteCount];
        Buffer.BlockCopy(frame, WordDataOffset, result, 0, expectedDataByteCount);
        return result;
    }

    /// <summary>워드 쓰기 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 SPH 응답 프레임입니다.</param>
    /// <param name="expectedConnectionId">기대하는 SX-Bus 연결 스테이션 ID입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedConnectionId)
    {
        ValidateResponseHeader(frame, expectedConnectionId, WriteCommandCode);
    }

    private static void ValidateResponseHeader(byte[] frame, byte expectedConnectionId, byte expectedCommand)
    {
        if (frame is null || frame.Length < HeaderByteLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SPH 응답 헤더 길이가 부족합니다.");
        }

        if (frame[0] != FrameStartIdentifier)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"올바르지 않은 SPH 헤더 식별자입니다: 0x{frame[0]:X2}");
        }

        byte endCode = frame[EndCodeOffset];
        if (endCode != NormalEndCode)
        {
            string errorMessage = GetErrorMessage(endCode);
            throw new PlcDeviceException(endCode, $"SPH 장치 오류 (코드 0x{endCode:X2}): {errorMessage}");
        }

        ValidateEchoAndLength(frame, expectedConnectionId, expectedCommand);
    }

    private static void ValidateEchoAndLength(byte[] frame, byte expectedConnectionId, byte expectedCommand)
    {
        byte receivedConnectionId = frame[ConnectionIdOffset];
        if (receivedConnectionId != expectedConnectionId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 연결 ID 불일치: 예상 0x{expectedConnectionId:X2}, 수신 0x{receivedConnectionId:X2}");
        }

        byte receivedCommand = frame[CommandEchoOffset];
        if (receivedCommand != expectedCommand)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 명령 에코 불일치: 예상 0x{expectedCommand:X2}, 수신 0x{receivedCommand:X2}");
        }

        ushort declaredPayloadLength = BinaryBuffer.ReadUInt16LittleEndian(frame, PayloadLengthOffset);
        int actualPayloadLength = frame.Length - HeaderByteLength;
        if (actualPayloadLength < declaredPayloadLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 페이로드 길이가 선언된 크기({declaredPayloadLength})보다 작습니다. (실제: {actualPayloadLength})");
        }
    }

    private static string GetErrorMessage(byte endCode) => endCode switch
    {
        >= 0x10 and <= 0x2F => "CPU 실행 / 로더 명령 / 인터록 처리 오류",
        >= 0x40 and <= 0x45 => "메모리 영역 코드 또는 주소 범위 초과 오류",
        >= 0xA0 and <= 0xA5 => "SX-Bus 모듈 통신 또는 라우팅 오류",
        _ => "알 수 없는 MICREX-SX SPH 오류",
    };
}
