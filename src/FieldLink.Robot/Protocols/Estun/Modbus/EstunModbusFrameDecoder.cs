using System;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun Modbus TCP(MBAP) 응답 프레임 경계를 판정하는 디코더입니다.</summary>
public sealed class EstunModbusFrameDecoder : IFrameDecoder
{
    private const int MbapPrefixLength = 6;
    private const int LengthFieldOffset = 4;
    private const int MinimumRemainingBytes = 2;
    private const int MaximumFrameLength = 65536;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < MbapPrefixLength || received.Array is not byte[] buffer)
        {
            frameLength = 0;
            return false;
        }

        ushort remainingLength = RobotBinaryBuffer.ReadUInt16BigEndian(buffer, received.Offset + LengthFieldOffset);
        if (remainingLength < MinimumRemainingBytes)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MBAP 길이 필드({remainingLength})가 유효한 최소 PDU 크기보다 작습니다.");
        }

        int totalLength = MbapPrefixLength + remainingLength;
        if (totalLength > MaximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MBAP 전체 프레임 길이({totalLength})가 최대 허용 범위를 초과했습니다.");
        }

        if (received.Count < totalLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = totalLength;
        return true;
    }
}
