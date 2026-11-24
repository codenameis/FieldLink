using System;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC SRTP/SNPX 응답 프레임과 전체 스냅샷을 해석하는 순수 정적 파서입니다.</summary>
public static partial class FanucResponseParser
{
    private const int HeaderLength = 56;
    private const int PayloadLengthOffset = 4;
    private const int InlinePayloadOffset = 44;
    private const int InlineMaxBytes = 6;
    private const byte ExtendedReadSuccessCode = 0x94;
    private const byte InlineSuccessCode = 0xD4;
    private const int StatusByteOffset = 31;

    /// <summary>FANUC 읽기 응답 프레임에서 요청 바이트 수만큼의 페이로드를 추출합니다.</summary>
    public static byte[] ParseReadResponse(byte[] response, int expectedByteCount)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (expectedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedByteCount), expectedByteCount, "기대 바이트 수는 0 이상이어야 합니다.");
        }

        if (response.Length < HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 응답 길이가 헤더 크기({HeaderLength} 바이트)보다 짧습니다. (실제: {response.Length} 바이트)",
                response);
        }

        byte status = response[StatusByteOffset];
        if (status == InlineSuccessCode)
        {
            return ExtractInlinePayload(response, expectedByteCount);
        }

        if (status == ExtendedReadSuccessCode)
        {
            return ExtractExtendedPayload(response, expectedByteCount);
        }

        throw new RobotDeviceException(
            status,
            $"FANUC 읽기 요청이 장치 오류(0x{status:X2})로 거절되었습니다.",
            response);
    }

    /// <summary>FANUC 쓰기 응답 프레임의 성공 여부를 검증합니다.</summary>
    public static void ParseWriteResponse(byte[] response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.Length < HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 쓰기 응답 길이가 헤더 크기({HeaderLength} 바이트)보다 짧습니다. (실제: {response.Length} 바이트)",
                response);
        }

        byte status = response[StatusByteOffset];
        if (status != InlineSuccessCode)
        {
            throw new RobotDeviceException(
                status,
                $"FANUC 쓰기 요청이 장치 오류(0x{status:X2})로 거절되었습니다.",
                response);
        }
    }

    private static byte[] ExtractInlinePayload(byte[] response, int expectedByteCount)
    {
        if (expectedByteCount > InlineMaxBytes || InlinePayloadOffset + expectedByteCount > response.Length)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 인라인 응답 크기가 기대 바이트({expectedByteCount})를 담기에 유효하지 않습니다.",
                response);
        }

        byte[] result = new byte[expectedByteCount];
        Array.Copy(response, InlinePayloadOffset, result, 0, expectedByteCount);
        return result;
    }

    private static byte[] ExtractExtendedPayload(byte[] response, int expectedByteCount)
    {
        ushort payloadLength = Conversion.RobotBinaryBuffer.ReadUInt16LittleEndian(response, PayloadLengthOffset);
        if (response.Length < HeaderLength + payloadLength || payloadLength < expectedByteCount)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"FANUC 확장 응답 길이({payloadLength})가 기대 바이트({expectedByteCount})보다 작습니다.",
                response);
        }

        int copyLength = expectedByteCount > 0 ? expectedByteCount : payloadLength;
        byte[] result = new byte[copyLength];
        Array.Copy(response, HeaderLength, result, 0, copyLength);
        return result;
    }
}
