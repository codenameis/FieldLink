using System;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc SRTP 응답 프레임을 검증하고 데이터를 추출하는 파서입니다. (사양서 §2)</summary>
public static class GESrtpResponseParser
{
    /// <summary>세션 초기화 응답(타입 0x01)을 검증합니다. (사양서 §1.1)</summary>
    public static void ValidateInitResponse(byte[] frame)
    {
        if (frame is null || frame.Length < 56)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SRTP 초기화 응답 길이가 부족합니다.");
        }

        if (frame[0] != 0x01)
        {
            throw new PlcException(PlcErrorKind.ConnectionFailed, $"SRTP 세션 초기화 거절 (패킷 타입: 0x{frame[0]:X2})");
        }
    }

    /// <summary>읽기 응답을 검증하고 데이터를 추출합니다. (사양서 §2.3)</summary>
    public static byte[] ParseReadResponse(byte[] frame, ushort expectedSequenceId, int expectedByteCount)
    {
        ValidateResponseHeader(frame, expectedSequenceId);

        byte msgType = frame[31];
        if (msgType == 0xD4)
        {
            // 6바이트 이하 피기백 응답 (오프셋 44~49)
            if (expectedByteCount > 6)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"피기백 응답 범위 초과: {expectedByteCount}바이트");
            }

            byte[] result = new byte[expectedByteCount];
            Array.Copy(frame, 44, result, 0, expectedByteCount);
            return result;
        }

        if (msgType == 0x94)
        {
            // 확장 페이로드 응답 (오프셋 56 이후)
            int payloadLen = frame[4] | (frame[5] << 8);
            if (payloadLen < expectedByteCount || frame.Length < 56 + expectedByteCount)
            {
                throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 페이로드 길이 부족: 필요 {expectedByteCount}, 수신 {payloadLen}");
            }

            byte[] result = new byte[expectedByteCount];
            Array.Copy(frame, 56, result, 0, expectedByteCount);
            return result;
        }

        throw new PlcException(PlcErrorKind.InvalidResponse, $"지원하지 않는 SRTP 메시지 타입입니다: 0x{msgType:X2}");
    }

    /// <summary>쓰기 응답을 검증합니다.</summary>
    public static void VerifyWriteResponse(byte[] frame, ushort expectedSequenceId)
    {
        ValidateResponseHeader(frame, expectedSequenceId);
    }

    private static void ValidateResponseHeader(byte[] frame, ushort expectedSequenceId)
    {
        if (frame is null || frame.Length < 56)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, "SRTP 응답 프레임 헤더 길이가 부족합니다.");
        }

        if (frame[0] != 0x03)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 응답 타입 불일치 (예상 0x03, 수신 0x{frame[0]:X2})");
        }

        ushort seq = (ushort)(frame[2] | (frame[3] << 8));
        if (seq != expectedSequenceId)
        {
            throw new PlcException(PlcErrorKind.InvalidResponse, $"SRTP 시퀀스 ID 불일치 (예상 {expectedSequenceId}, 수신 {seq})");
        }

        ushort status = (ushort)(frame[42] | (frame[43] << 8));
        if (status != 0)
        {
            throw new PlcDeviceException(status, $"SRTP 장치 처리 오류 (상태 코드: 0x{status:X4})");
        }
    }
}
