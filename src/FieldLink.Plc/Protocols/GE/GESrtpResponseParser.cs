using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 응답 프레임을 검증하고 데이터를 추출하는 정적 파서입니다. (사양서 §2)</summary>
public static class GESrtpResponseParser
{
    private const int HeaderLength = 56;
    private const int PacketTypeOffset = 0;
    private const int SequenceIdOffset = 2;
    private const int ExtendedPayloadLengthOffset = 4;
    private const int MessageTypeOffset = 31;
    private const int StatusCodeOffset = 42;
    private const int PiggybackDataOffset = 44;
    private const int MaxPiggybackByteCount = 6;
    private const int ExtendedPayloadOffset = 56;

    private const byte InitResponsePacketType = 0x01;
    private const byte ServiceResponsePacketType = 0x03;
    private const byte ShortAckMessageType = 0xD4;
    private const byte ExtendedAckMessageType = 0x94;
    private const byte ErrorResponseMessageType = 0xD1;

    /// <summary>세션 초기화 응답(패킷 타입 0x01)을 검증합니다. (사양서 §1.1)</summary>
    /// <param name="responseFrame">수신한 응답 프레임 바이트 배열입니다.</param>
    /// <exception cref="PlcException">응답 길이가 56바이트가 아니거나 초기화 거절 응답일 때.</exception>
    public static void ValidateInitResponse(byte[] responseFrame)
    {
        if (responseFrame is null || responseFrame.Length != HeaderLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 초기화 응답 프레임 길이가 {HeaderLength}바이트여야 합니다. (수신: {responseFrame?.Length ?? 0}바이트)");
        }

        if (responseFrame[PacketTypeOffset] != InitResponsePacketType)
        {
            throw new PlcException(
                PlcErrorKind.ConnectionFailed,
                $"SRTP 세션 초기화가 거절되었습니다 (패킷 타입: 0x{responseFrame[PacketTypeOffset]:X2}).");
        }
    }

    /// <summary>읽기 응답 프레임을 검증하고 요청한 바이트 수만큼 데이터를 추출합니다. (사양서 §2.3)</summary>
    /// <param name="responseFrame">수신한 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedSequenceId">요청에 사용한 순번 식별자입니다.</param>
    /// <param name="expectedByteCount">추출할 데이터 바이트 수입니다.</param>
    /// <returns>추출된 응답 데이터 바이트 배열입니다.</returns>
    /// <exception cref="PlcDeviceException">장치가 에러(0xD1 또는 상태 코드 오류)로 요청을 거부했을 때.</exception>
    /// <exception cref="PlcException">프레임이 손상되었거나 형식이 맞지 않을 때.</exception>
    public static byte[] ParseReadResponse(byte[] responseFrame, ushort expectedSequenceId, int expectedByteCount)
    {
        if (expectedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedByteCount), expectedByteCount, "기대 바이트 수는 0 이상이어야 합니다.");
        }

        ValidateResponseHeader(responseFrame, expectedSequenceId);

        byte messageType = responseFrame[MessageTypeOffset];
        if (messageType == ShortAckMessageType)
        {
            return ExtractShortPiggybackData(responseFrame, expectedByteCount);
        }

        if (messageType == ExtendedAckMessageType)
        {
            return ExtractExtendedPayloadData(responseFrame, expectedByteCount);
        }

        throw new PlcException(PlcErrorKind.InvalidResponse, $"지원하지 않는 SRTP 메시지 타입입니다: 0x{messageType:X2}");
    }

    /// <summary>쓰기 응답 프레임을 검증합니다. (사양서 §2.3)</summary>
    /// <param name="responseFrame">수신한 응답 프레임 바이트 배열입니다.</param>
    /// <param name="expectedSequenceId">요청에 사용한 순번 식별자입니다.</param>
    /// <exception cref="PlcDeviceException">장치가 에러(0xD1 또는 상태 코드 오류)로 요청을 거부했을 때.</exception>
    /// <exception cref="PlcException">프레임이 손상되었거나 형식이 맞지 않을 때.</exception>
    public static void VerifyWriteResponse(byte[] responseFrame, ushort expectedSequenceId)
    {
        ValidateResponseHeader(responseFrame, expectedSequenceId);

        byte messageType = responseFrame[MessageTypeOffset];
        if (messageType == ShortAckMessageType)
        {
            ushort extendedLength = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, ExtendedPayloadLengthOffset);
            if (extendedLength != 0)
            {
                throw new PlcException(
                    PlcErrorKind.InvalidResponse,
                    $"SRTP 단문 쓰기 응답에 확장 페이로드({extendedLength}바이트)가 포함될 수 없습니다.");
            }

            return;
        }

        if (messageType != ExtendedAckMessageType)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"지원하지 않는 SRTP 쓰기 응답 메시지 타입입니다: 0x{messageType:X2}");
        }
    }

    private static void ValidateResponseHeader(byte[] responseFrame, ushort expectedSequenceId)
    {
        if (responseFrame is null || responseFrame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SRTP 응답 프레임 헤더 길이가 부족합니다.");
        }

        if (responseFrame[PacketTypeOffset] != ServiceResponsePacketType)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 응답 패킷 타입이 일치하지 않습니다 (예상: 0x03, 수신: 0x{responseFrame[PacketTypeOffset]:X2}).");
        }

        ushort extendedLength = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, ExtendedPayloadLengthOffset);
        int expectedFrameLength = HeaderLength + extendedLength;
        if (responseFrame.Length != expectedFrameLength)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 응답 프레임 길이가 헤더 확장 길이와 일치하지 않습니다 (기대: {expectedFrameLength}, 수신: {responseFrame.Length}).");
        }

        ushort sequenceId = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, SequenceIdOffset);
        if (sequenceId != expectedSequenceId)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 시퀀스 ID가 일치하지 않습니다 (예상: {expectedSequenceId}, 수신: {sequenceId}).");
        }

        byte messageType = responseFrame[MessageTypeOffset];
        ushort statusCode = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, StatusCodeOffset);
        if (messageType == ErrorResponseMessageType)
        {
            ThrowDeviceErrorForRejectedService(statusCode);
        }

        if (statusCode != (ushort)GESrtpErrorCode.Success)
        {
            var errorCode = (GESrtpErrorCode)statusCode;
            throw new PlcDeviceException(statusCode, errorCode.GetDescription());
        }
    }

    private static void ThrowDeviceErrorForRejectedService(ushort statusCode)
    {
        ushort effectiveCode = statusCode != (ushort)GESrtpErrorCode.Success
            ? statusCode
            : (ushort)GESrtpErrorCode.ServiceRejected;
        var errorCode = (GESrtpErrorCode)effectiveCode;
        throw new PlcDeviceException(effectiveCode, errorCode.GetDescription());
    }

    private static byte[] ExtractShortPiggybackData(byte[] responseFrame, int expectedByteCount)
    {
        ushort extendedLength = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, ExtendedPayloadLengthOffset);
        if (extendedLength != 0)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 단문 응답에 확장 페이로드({extendedLength}바이트)가 포함될 수 없습니다.");
        }

        if (expectedByteCount > MaxPiggybackByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 단문 피기백 응답 범위를 초과했습니다 (요청: {expectedByteCount}바이트, 한도: {MaxPiggybackByteCount}바이트).");
        }

        byte[] result = new byte[expectedByteCount];
        Buffer.BlockCopy(responseFrame, PiggybackDataOffset, result, 0, expectedByteCount);
        return result;
    }

    private static byte[] ExtractExtendedPayloadData(byte[] responseFrame, int expectedByteCount)
    {
        int payloadByteCount = BinaryBuffer.ReadUInt16LittleEndian(responseFrame, ExtendedPayloadLengthOffset);
        if (payloadByteCount != expectedByteCount || responseFrame.Length != ExtendedPayloadOffset + expectedByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"SRTP 확장 페이로드 길이가 일치하지 않습니다 (기대: {expectedByteCount}바이트, 수신: {payloadByteCount}바이트).");
        }

        byte[] result = new byte[expectedByteCount];
        Buffer.BlockCopy(responseFrame, ExtendedPayloadOffset, result, 0, expectedByteCount);
        return result;
    }
}
