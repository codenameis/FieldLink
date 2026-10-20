using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus TCP MBAP 헤더의 길이 필드를 해석하여 프레임 경계를 판정하는 디코더입니다. (사양서 §1, §3-0)</summary>
public sealed class ModbusTcpFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 6;
    private const int LengthFieldOffset = 4;
    private const int LengthFieldByteCount = 2;
    private const int LengthAdjustment = 6;
    private const int MaximumModbusTcpAduLength = 260; // MBAP 헤더 7바이트 + PDU 최대 253바이트

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
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
