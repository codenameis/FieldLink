using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 §2)</summary>
public static class ToyopucResponseParser
{
    private const int HeaderLength = 4;
    private const int FrameTypeOffset = 0;
    private const int ResponseCodeOffset = 1;
    private const int BodyLengthOffset = 2;
    private const int CommandEchoOffset = 4;
    private const int DataPayloadOffset = 5;
    private const int MinimumReadBitFrameLength = 6;
    private const int BytesPerWord = 2;

    private const byte ExpectedResponseFrameType = 0x80;
    private const byte SuccessResponseCode = 0x00;
    private const byte ReadBitCommandEcho = 0x20;

    /// <summary>단일 비트 읽기 응답을 검증하고 1바이트(0 또는 1)를 반환합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <returns>읽어온 비트 값(0 또는 1)입니다.</returns>
    public static byte ParseReadBit(byte[] responseFrame)
    {
        ValidateResponseHeader(responseFrame, expectedCommand: ReadBitCommandEcho);
        if (responseFrame.Length < MinimumReadBitFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "비트 응답 데이터 길이가 부족합니다.");
        }

        return (byte)(responseFrame[DataPayloadOffset] != 0 ? 1 : 0);
    }

    /// <summary>연속 워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 반환합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedCommand">기대하는 명령 에코(기본 0x1C, 확장 0x94)입니다.</param>
    /// <param name="expectedWordCount">기대하는 워드 수입니다.</param>
    /// <returns>추출된 리틀 엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(byte[] responseFrame, byte expectedCommand, ushort expectedWordCount)
    {
        ValidateResponseHeader(responseFrame, expectedCommand);
        int expectedByteCount = expectedWordCount * BytesPerWord;
        int requiredFrameLength = DataPayloadOffset + expectedByteCount;
        if (responseFrame.Length < requiredFrameLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"워드 응답 데이터 길이 부족: 필요 {requiredFrameLength}, 수신 {responseFrame.Length}");
        }

        byte[] wordBytes = new byte[expectedByteCount];
        Buffer.BlockCopy(responseFrame, DataPayloadOffset, wordBytes, 0, expectedByteCount);
        return wordBytes;
    }

    /// <summary>쓰기 명령 응답을 검증합니다.</summary>
    /// <param name="responseFrame">수신된 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedCommand">기대하는 명령 에코(0x21, 0x1D, 0x95)입니다.</param>
    public static void VerifyWriteResponse(byte[] responseFrame, byte expectedCommand)
    {
        ValidateResponseHeader(responseFrame, expectedCommand);
    }

    private static void ValidateResponseHeader(byte[] responseFrame, byte expectedCommand)
    {
        if (responseFrame is null || responseFrame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "TOYOPUC 응답 프레임 헤더 길이가 부족합니다.");
        }

        if (responseFrame[FrameTypeOffset] != ExpectedResponseFrameType)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"올바르지 않은 TOYOPUC 응답 프레임 타입: 0x{responseFrame[FrameTypeOffset]:X2}");
        }

        ushort declaredBodyLength = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, BodyLengthOffset);
        int actualBodyLength = responseFrame.Length - HeaderLength;
        if (actualBodyLength < declaredBodyLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"TOYOPUC 프레임 본문 길이({actualBodyLength})가 선언된 길이({declaredBodyLength})보다 작습니다.");
        }

        byte responseCode = responseFrame[ResponseCodeOffset];
        if (responseCode != SuccessResponseCode)
        {
            byte errorCode = responseFrame.Length >= DataPayloadOffset ? responseFrame[CommandEchoOffset] : responseCode;
            string errorMessage = GetErrorMessage(errorCode);
            throw new PlcDeviceException(errorCode, $"TOYOPUC 장치 오류 (코드 0x{errorCode:X2}): {errorMessage}");
        }

        if (declaredBodyLength < 1 || responseFrame.Length < DataPayloadOffset)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "TOYOPUC 정상 응답 프레임에 명령 에코 바이트가 누락되었습니다.");
        }

        if (responseFrame[CommandEchoOffset] != expectedCommand)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"명령 에코 불일치: 예상 0x{expectedCommand:X2}, 수신 0x{responseFrame[CommandEchoOffset]:X2}");
        }
    }

    private static string GetErrorMessage(byte errorCode) => errorCode switch
    {
        0x11 => "CPU 모듈 하드웨어 이상",
        0x20 => "중계 명령 고정 데이터 오류",
        0x21 => "전송 바이트 수 오류 (헤더-본문 길이 불일치)",
        0x23 => "정의되지 않은 명령 코드 오류",
        0x24 => "서브명령 코드 오류",
        0x25 => "명령 포맷 데이터 바이트 오류",
        0x26 => "기능 호출 피연산자 개수 오류",
        0x31 => "시퀀스 RUN 중 쓰기 금지 / 메모리 보호 오류",
        0x32 => "정의되지 않은 프로그램 번호(PRG) 오류",
        0x34 => "파라미터 설정에 의한 접근 금지",
        0x35 => "타 스테이션의 배타 제어 점유",
        0x3E => "CPU 리셋 상태로 인한 명령 실행 불가",
        0x3F => "CPU 정지(STOP) 상태로 인한 명령 실행 불가",
        0x40 => "주소 범위 초과 오류",
        0x41 => "워드 또는 바이트 개수 범위 초과 오류",
        _ => "알 수 없는 TOYOPUC 오류",
    };
}
