using System;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus ASCII 프레임(콜론 ':' 시작, CR LF 종료)의 경계를 판정하는 디코더입니다. (사양서 §1, §3-0)</summary>
public sealed class ModbusAsciiFrameDecoder : IFrameDecoder
{
    private const int MaximumModbusAsciiLength = 513; // 사양서 §3-0 최대 513문자
    private static readonly byte[] StartDelimiter = new byte[] { 0x3A }; // ':'
    private static readonly byte[] EndDelimiter = new byte[] { 0x0D, 0x0A }; // CR LF

    private readonly DelimitedFrameDecoder _innerDecoder = new(
        EndDelimiter,
        MaximumModbusAsciiLength,
        StartDelimiter);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
