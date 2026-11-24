using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc SRTP 응답 프레임을 검증하고 데이터를 추출하는 파서입니다. (사양서 §2)</summary>
public static class GESrtpResponseParser
{
    private const int HeaderLength = 56;
    private const int SequenceIdOffset = 2;
    private const int PayloadLengthOffset = 4;
    private const int MessageTypeOffset = 31;
    private const int StatusOffset = 42;
    private const int PiggybackDataOffset = 44;
    private const int PiggybackMaxBytes = 6;
    private const byte InitResponseType = 0x01;
    private const byte DataResponseType = 0x03;
    private const byte ShortAckMessageType = 0xD4;
    private const byte ExtendedAckMessageType = 0x94;
    private const ushort SuccessStatus = 0;

    /// <summary>세션 초기화 응답(타입 0x01)을 검증합니다. (사양서 §1.1)</summary>
    public static void ValidateInitResponse(byte[] frame)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SRTP 초기화 응답 길이가 부족합니다.");
        }

        if (frame[0] != InitResponseType)
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, $"SRTP 세션 초기화 거절 (패킷 타입: 0x{frame[0]:X2})");
        }
    }

    /// <summary>읽기 응답을 검증하고 데이터를 추출합니다. (사양서 §2.3)</summary>
    public static byte[] ParseReadResponse(byte[] frame, ushort expectedSequenceId, int expectedByteCount)
    {
        if (expectedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedByteCount), expectedByteCount, "기대 바이트 수는 0 이상이어야 합니다.");
        }

        ValidateResponseHeader(frame, expectedSequenceId);

        byte msgType = frame[MessageTypeOffset];
        if (msgType == ShortAckMessageType)
        {
            // 6바이트 이하 피기백 응답 (오프셋 44~49)
            if (expectedByteCount > PiggybackMaxBytes)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"피기백 응답 범위 초과: {expectedByteCount}바이트");
            }

            byte[] result = new byte[expectedByteCount];
            Buffer.BlockCopy(frame, PiggybackDataOffset, result, 0, expectedByteCount);
            return result;
        }

        if (msgType == ExtendedAckMessageType)
        {
            // 확장 페이로드 응답 (오프셋 56 이후)
            int payloadLen = BinaryBuffer.ReadUInt16LE(frame, PayloadLengthOffset);
            if (payloadLen < expectedByteCount || frame.Length < HeaderLength + expectedByteCount)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 페이로드 길이 부족: 필요 {expectedByteCount}, 수신 {payloadLen}");
            }

            byte[] result = new byte[expectedByteCount];
            Buffer.BlockCopy(frame, HeaderLength, result, 0, expectedByteCount);
            return result;
        }

        throw new PlcException(PlcErrorKind.InvalidResponse, $"지원하지 않는 SRTP 메시지 타입입니다: 0x{msgType:X2}");
    }

    /// <summary>쓰기 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, ushort expectedSequenceId)
    {
        ValidateResponseHeader(frame, expectedSequenceId);

        byte msgType = frame[MessageTypeOffset];
        if (msgType != ShortAckMessageType && msgType != ExtendedAckMessageType)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"지원하지 않는 SRTP 쓰기 응답 메시지 타입입니다: 0x{msgType:X2}");
        }
    }

    private static void ValidateResponseHeader(byte[] frame, ushort expectedSequenceId)
    {
        if (frame is null || frame.Length < HeaderLength)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SRTP 응답 프레임 헤더 길이가 부족합니다.");
        }

        if (frame[0] != DataResponseType)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 응답 타입 불일치 (예상 0x{DataResponseType:X2}, 수신 0x{frame[0]:X2})");
        }

        ushort seq = BinaryBuffer.ReadUInt16LE(frame, SequenceIdOffset);
        if (seq != expectedSequenceId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 시퀀스 ID 불일치 (예상 {expectedSequenceId}, 수신 {seq})");
        }

        ushort status = BinaryBuffer.ReadUInt16LE(frame, StatusOffset);
        if (status != SuccessStatus)
        {
            throw new PlcDeviceException(status, $"SRTP 장치 처리 오류 (상태 코드: 0x{status:X4})");
        }
    }
}
