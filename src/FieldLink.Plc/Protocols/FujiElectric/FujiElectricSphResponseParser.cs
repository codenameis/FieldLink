using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part C §C.2)</summary>
public static class FujiElectricSphResponseParser
{
    private const byte FrameHeader = 0xFB;
    private const byte SuccessStatus = 0x00;
    private const byte CommandReadWords = 0x00;
    private const byte CommandWriteWords = 0x01;
    private const int HeaderLength = 20;
    private const int DataPayloadOffset = 26;
    private const int BytesPerWord = 2;
    private const int StatusOffset = 4;
    private const int ConnectionIdOffset = 6;
    private const int CommandEchoOffset = 14;
    private const int PayloadLengthOffset = 18;

    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    public static byte[] ParseReadWords(byte[] frame, byte expectedConnectionId, ushort expectedWordCount)
    {
        ValidateResponseHeader(frame, expectedConnectionId, expectedCommand: CommandReadWords);

        int expectedBytes = expectedWordCount * BytesPerWord;
        if (frame.Length < DataPayloadOffset + expectedBytes)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SPH 응답 데이터 길이가 부족합니다: 필요 {DataPayloadOffset + expectedBytes}, 수신 {frame.Length}");
        }

        byte[] result = new byte[expectedBytes];
        Buffer.BlockCopy(frame, DataPayloadOffset, result, 0, expectedBytes);
        return result;
    }

    /// <summary>워드 쓰기 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, byte expectedConnectionId)
    {
        ValidateResponseHeader(frame, expectedConnectionId, expectedCommand: CommandWriteWords);
    }

    private static void ValidateResponseHeader(byte[] frame, byte expectedConnectionId, byte expectedCommand)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SPH 응답 헤더 길이가 부족합니다.");
        }

        if (frame[0] != FrameHeader)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"올바르지 않은 SPH 헤더 식별자입니다: 0x{frame[0]:X2}");
        }

        byte status = frame[StatusOffset];
        if (status != SuccessStatus)
        {
            string message = GetErrorMessage(status);
            throw new PlcDeviceException(status, $"SPH 장치 오류 (코드 0x{status:X2}): {message}");
        }

        if (frame[ConnectionIdOffset] != expectedConnectionId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SPH 연결 ID 불일치: 예상 0x{expectedConnectionId:X2}, 수신 0x{frame[ConnectionIdOffset]:X2}");
        }

        if (frame[CommandEchoOffset] != expectedCommand)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SPH 명령 에코 불일치: 예상 0x{expectedCommand:X2}, 수신 0x{frame[CommandEchoOffset]:X2}");
        }

        ushort declaredPayloadLength = BinaryBuffer.ReadUInt16LE(frame, PayloadLengthOffset);
        if (frame.Length - HeaderLength < declaredPayloadLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SPH 응답 페이로드 길이가 선언된 크기({declaredPayloadLength})보다 작습니다. (실제: {frame.Length - HeaderLength})");
        }
    }

    private static string GetErrorMessage(byte code) => code switch
    {
        >= 0x10 and <= 0x2F => "CPU 실행 / 로더 명령 / 인터록 처리 오류",
        >= 0x40 and <= 0x45 => "메모리 영역 코드 또는 주소 범위 초과 오류",
        >= 0xA0 and <= 0xA5 => "SX-Bus 모듈 통신 또는 라우팅 오류",
        _ => "알 수 없는 MICREX-SX SPH 오류",
    };
}
