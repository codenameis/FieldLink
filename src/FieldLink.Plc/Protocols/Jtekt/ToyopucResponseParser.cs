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
    private const int ExpectedReadBitFrameLength = 6;
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
        if (responseFrame.Length != ExpectedReadBitFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"비트 응답 프레임 길이가 올바르지 않습니다: 기대 {ExpectedReadBitFrameLength}, 수신 {responseFrame.Length}");
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
        if (expectedWordCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWordCount), expectedWordCount, "기대 워드 수는 1 이상이어야 합니다.");
        }

        ValidateResponseHeader(responseFrame, expectedCommand);
        int expectedByteCount = expectedWordCount * BytesPerWord;
        int requiredFrameLength = DataPayloadOffset + expectedByteCount;
        if (responseFrame.Length != requiredFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"워드 응답 데이터 길이 불일치: 기대 {requiredFrameLength}, 수신 {responseFrame.Length}");
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
        if (responseFrame.Length != DataPayloadOffset)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"쓰기 응답 프레임 길이가 올바르지 않습니다: 기대 {DataPayloadOffset}, 수신 {responseFrame.Length}");
        }
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
        if (actualBodyLength != declaredBodyLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"TOYOPUC 프레임 본문 길이({actualBodyLength})가 선언된 길이({declaredBodyLength})와 일치하지 않습니다.");
        }

        byte responseCode = responseFrame[ResponseCodeOffset];
        if (responseCode != SuccessResponseCode)
        {
            byte errorCode = responseFrame.Length >= DataPayloadOffset ? responseFrame[CommandEchoOffset] : responseCode;
            string errorMessage = ((ToyopucErrorCode)errorCode).GetDescription();
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
}
