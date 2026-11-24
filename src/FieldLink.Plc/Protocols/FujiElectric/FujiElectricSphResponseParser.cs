using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 응답 프레임을 검증하고 데이터를 파싱합니다. (사양서 Part C §C.2)</summary>
public static class FujiElectricSphResponseParser
{
    private const byte FrameStartIdentifier = 0xFB;
    private const byte ExpectedVersionControl = 0x80;
    private const byte ExpectedSingleFrameType = 0x80;
    private const byte ExpectedFrameType = 0x7B;
    private const byte ExpectedBlockCount = 0x01;
    private const byte NormalEndCode = 0x00;
    private const byte ReadCommandCode = 0x00;
    private const byte WriteCommandCode = 0x01;

    private const int HeaderByteLength = 20;
    private const int DescriptorByteLength = 6;
    private const int WriteResponseFrameLength = HeaderByteLength + DescriptorByteLength;
    private const int VersionControlOffset = 1;
    private const int SingleFrameTypeOffset = 2;
    private const int EndCodeOffset = 4;
    private const int FrameTypeOffset = 5;
    private const int ConnectionIdOffset = 6;
    private const int CommandEchoOffset = 14;
    private const int BlockCountOffset = 17;
    private const int PayloadLengthOffset = 18;
    private const int AreaCodeOffset = 20;
    private const int WordAddressLowOffset = 21;
    private const int WordAddressMiddleOffset = 22;
    private const int WordAddressHighOffset = 23;
    private const int WordCountOffset = 24;
    private const int WordDataOffset = 26;
    private const int BytesPerWord = 2;
    private const int BitsShift8 = 8;
    private const int BitsShift16 = 16;

    /// <summary>워드 읽기 응답을 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 SPH 응답 프레임입니다.</param>
    /// <param name="expectedConnectionId">기대하는 SX-Bus 연결 스테이션 ID입니다.</param>
    /// <param name="expectedWordCount">요청한 워드 개수(1 이상)입니다.</param>
    /// <returns>추출된 리틀엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(byte[] frame, byte expectedConnectionId, ushort expectedWordCount)
    {
        if (expectedWordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWordCount), expectedWordCount, "워드 개수는 1 이상이어야 합니다.");
        }

        ValidateResponseHeader(frame, expectedConnectionId, ReadCommandCode);
        int expectedDataByteCount = ValidateExactReadFrameLength(frame, expectedWordCount);

        ushort echoedWordCount = BinaryBuffer.ReadUInt16LittleEndian(frame, WordCountOffset);
        if (echoedWordCount != expectedWordCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 워드 개수 불일치: 예상 {expectedWordCount}, 수신 {echoedWordCount}");
        }

        return ExtractWordBytes(frame, expectedDataByteCount);
    }

    /// <summary>워드 읽기 응답의 헤더와 주소 디스크립터 에코를 검증하고 리틀엔디언 바이트 배열을 추출합니다.</summary>
    /// <param name="frame">수신된 SPH 응답 프레임입니다.</param>
    /// <param name="expectedConnectionId">기대하는 SX-Bus 연결 스테이션 ID입니다.</param>
    /// <param name="expectedAddress">요청한 시작 워드 주소입니다.</param>
    /// <param name="expectedWordCount">요청한 워드 개수(1 이상)입니다.</param>
    /// <returns>추출된 리틀엔디언 워드 데이터 바이트 배열입니다.</returns>
    public static byte[] ParseReadWords(
        byte[] frame,
        byte expectedConnectionId,
        FujiElectricSphAddress expectedAddress,
        ushort expectedWordCount)
    {
        if (expectedAddress is null)
        {
            throw new ArgumentNullException(nameof(expectedAddress));
        }

        if (expectedWordCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedWordCount), expectedWordCount, "워드 개수는 1 이상이어야 합니다.");
        }

        ValidateResponseHeader(frame, expectedConnectionId, ReadCommandCode);
        int expectedDataByteCount = ValidateExactReadFrameLength(frame, expectedWordCount);
        ValidateEchoedDescriptor(frame, expectedAddress, expectedWordCount);

        return ExtractWordBytes(frame, expectedDataByteCount);
    }

    /// <summary>워드 쓰기 응답을 검증합니다.</summary>
    /// <param name="frame">수신된 SPH 응답 프레임입니다.</param>
    /// <param name="expectedConnectionId">기대하는 SX-Bus 연결 스테이션 ID입니다.</param>
    public static void VerifyWriteResponse(byte[] frame, byte expectedConnectionId)
    {
        ValidateResponseHeader(frame, expectedConnectionId, WriteCommandCode);

        if (frame.Length != WriteResponseFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 쓰기 응답 길이가 올바르지 않습니다: 예상 {WriteResponseFrameLength}, 수신 {frame.Length}");
        }
    }

    private static void ValidateResponseHeader(byte[] frame, byte expectedConnectionId, byte expectedCommand)
    {
        if (frame is null || frame.Length < HeaderByteLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SPH 응답 헤더 길이가 부족합니다.");
        }

        ValidateHeaderMarkers(frame);
        ValidateEchoAndLength(frame, expectedConnectionId, expectedCommand);

        byte endCode = frame[EndCodeOffset];
        if (endCode != NormalEndCode)
        {
            string errorMessage = GetErrorMessage(endCode);
            throw new PlcDeviceException(endCode, $"SPH 장치 오류 (코드 0x{endCode:X2}): {errorMessage}");
        }
    }

    private static void ValidateHeaderMarkers(byte[] frame)
    {
        if (frame[0] != FrameStartIdentifier)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"올바르지 않은 SPH 헤더 식별자입니다: 0x{frame[0]:X2}");
        }

        if (frame[VersionControlOffset] != ExpectedVersionControl || frame[SingleFrameTypeOffset] != ExpectedSingleFrameType)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"올바르지 않은 SPH 프로토콜 제어 바이트입니다: 0x{frame[VersionControlOffset]:X2} 0x{frame[SingleFrameTypeOffset]:X2}");
        }

        if (frame[FrameTypeOffset] != ExpectedFrameType || frame[BlockCountOffset] != ExpectedBlockCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"올바르지 않은 SPH 프레임 타입 또는 블록 수입니다: 타입 0x{frame[FrameTypeOffset]:X2}, 블록 {frame[BlockCountOffset]}");
        }
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
        if (actualPayloadLength != declaredPayloadLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 페이로드 길이가 선언된 크기({declaredPayloadLength})와 일치하지 않습니다. (실제: {actualPayloadLength})");
        }
    }

    private static int ValidateExactReadFrameLength(byte[] frame, ushort expectedWordCount)
    {
        int expectedDataByteCount = expectedWordCount * BytesPerWord;
        int expectedFrameLength = WordDataOffset + expectedDataByteCount;
        if (frame.Length != expectedFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 데이터 길이가 일치하지 않습니다: 예상 {expectedFrameLength}, 수신 {frame.Length}");
        }

        return expectedDataByteCount;
    }

    private static void ValidateEchoedDescriptor(byte[] frame, FujiElectricSphAddress expectedAddress, ushort expectedWordCount)
    {
        byte echoedAreaCode = frame[AreaCodeOffset];
        uint echoedWordAddress = (uint)(frame[WordAddressLowOffset]
            | (frame[WordAddressMiddleOffset] << BitsShift8)
            | (frame[WordAddressHighOffset] << BitsShift16));
        ushort echoedWordCount = BinaryBuffer.ReadUInt16LittleEndian(frame, WordCountOffset);

        if (echoedAreaCode != expectedAddress.AreaCode
            || echoedWordAddress != expectedAddress.WordAddress
            || echoedWordCount != expectedWordCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SPH 응답 주소 디스크립터 불일치: 영역 0x{echoedAreaCode:X2}, 주소 {echoedWordAddress}, 워드 수 {echoedWordCount}");
        }
    }

    private static byte[] ExtractWordBytes(byte[] frame, int expectedDataByteCount)
    {
        byte[] result = new byte[expectedDataByteCount];
        Buffer.BlockCopy(frame, WordDataOffset, result, 0, expectedDataByteCount);
        return result;
    }

    private static string GetErrorMessage(byte endCode) => endCode switch
    {
        >= 0x10 and <= 0x2F => "CPU 실행 / 로더 명령 / 인터록 처리 오류",
        >= 0x40 and <= 0x45 => "메모리 영역 코드 또는 주소 범위 초과 오류",
        >= 0xA0 and <= 0xA5 => "SX-Bus 모듈 통신 또는 라우팅 오류",
        _ => "알 수 없는 MICREX-SX SPH 오류",
    };
}
