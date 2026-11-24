using System;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKAVARPROXY 바이너리 응답 프레임을 검증하고 변수 값 바이트를 추출하는 순수 정적 파서입니다.</summary>
public static class KukaVarProxyResponseParser
{
    private const int HeaderLength = 4;
    private const int MinimumResponseLength = 8;
    private const int MessageIdOffset = 0;
    private const int BodyLengthOffset = 2;
    private const int FunctionCodeOffset = 4;
    private const int ValueLengthOffset = 5;
    private const int ValuePayloadOffset = 7;
    private const byte SuccessFlag = 0x01;

    /// <summary>응답 프레임의 메시지 ID·본문 길이·기능 코드·실행 결과 바이트를 검증한 뒤 변수 값 바이트 배열을 반환합니다.</summary>
    public static byte[] ParseValueBytes(
        byte[] frame,
        ushort expectedMessageId,
        KukaVarProxyFunctionCode expectedFunctionCode)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.Length < MinimumResponseLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"KUKAVARPROXY 응답 길이가 최소 크기({MinimumResponseLength} 바이트)보다 짧습니다. (실제: {frame.Length} 바이트)",
                frame);
        }

        ushort echoedMessageId = RobotBinaryBuffer.ReadUInt16BigEndian(frame, MessageIdOffset);
        if (echoedMessageId != expectedMessageId)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"KUKAVARPROXY 메시지 ID가 일치하지 않습니다. (기대: {expectedMessageId}, 수신: {echoedMessageId})",
                frame);
        }

        ushort bodyLength = RobotBinaryBuffer.ReadUInt16BigEndian(frame, BodyLengthOffset);
        if (bodyLength != frame.Length - HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"KUKAVARPROXY 본문 길이({bodyLength})가 실제 본문 크기({frame.Length - HeaderLength})와 일치하지 않습니다.",
                frame);
        }

        if (frame[FunctionCodeOffset] != (byte)expectedFunctionCode)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"KUKAVARPROXY 기능 코드가 일치하지 않습니다. (기대: 0x{(byte)expectedFunctionCode:X2}, 수신: 0x{frame[FunctionCodeOffset]:X2})",
                frame);
        }

        byte statusByte = frame[frame.Length - 1];
        if (statusByte != SuccessFlag)
        {
            throw new RobotDeviceException(
                statusByte,
                $"KUKAVARPROXY 변수 연산이 실패했습니다. (상태 코드: 0x{statusByte:X2})",
                frame);
        }

        ushort valueLength = RobotBinaryBuffer.ReadUInt16BigEndian(frame, ValueLengthOffset);
        if (ValuePayloadOffset + valueLength >= frame.Length)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"KUKAVARPROXY 응답 값 길이({valueLength})가 전체 프레임 크기({frame.Length})를 초과했습니다.",
                frame);
        }

        byte[] valueBytes = new byte[valueLength];
        Array.Copy(frame, ValuePayloadOffset, valueBytes, 0, valueLength);
        return valueBytes;
    }
}
