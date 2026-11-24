using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus TCP MBAP 헤더의 길이 필드를 해석하여 프레임 경계를 판정하는 디코더입니다. (사양서 §1, §3-0)</summary>
public sealed class ModbusTcpFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 6;
    private const int ProtocolIdOffset = 2;
    private const int LengthFieldOffset = 4;
    private const int LengthFieldByteCount = 2;
    private const int LengthAdjustment = 6;
    private const int MinimumLengthField = 3; // 유닛 ID(1) + 최소 응답 PDU(2)
    private const int MaximumModbusTcpAduLength = 260; // MBAP 헤더 7바이트 + PDU 최대 253바이트
    private const ushort ExpectedProtocolId = 0x0000;
    private const int ByteShift = 8;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        HeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: true,
        LengthAdjustment,
        MaximumModbusTcpAduLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < HeaderLength)
        {
            frameLength = 0;
            return false;
        }

        if (received.Array is not { } buffer)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        int offset = received.Offset;
        ushort protocolId = (ushort)((buffer[offset + ProtocolIdOffset] << ByteShift) | buffer[offset + ProtocolIdOffset + 1]);
        if (protocolId != ExpectedProtocolId)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"Modbus TCP 프로토콜 ID가 0x0000이 아닙니다. (수신: 0x{protocolId:X4})");
        }

        int lengthField = (buffer[offset + LengthFieldOffset] << ByteShift) | buffer[offset + LengthFieldOffset + 1];
        if (lengthField < MinimumLengthField)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MBAP 길이 필드({lengthField})는 최소 {MinimumLengthField} 이상이어야 합니다.");
        }

        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
