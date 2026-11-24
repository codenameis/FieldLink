using System;
using FieldLink.Plc.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 바이너리 헤더의 길이 필드(리틀 엔디언)로 프레임 경계를 판정하는 디코더입니다. (사양서 §1)</summary>
public sealed class MCBinaryFrameDecoder : IFrameDecoder
{
    private const int Header3ELength = 9;
    private const int Header4ELength = 13;
    private const int LengthFieldByteCount = 2;
    private const int MaxLengthFieldValue = 0xFFFF;
    private const int MinimumResponseBodyBytes = 2;
    private const int ReservedOffset4E = 4;
    private const ushort Response3ESubheader = 0x00D0;
    private const ushort Response4ESubheader = 0x00D4;
    private const ushort ReservedWordZero = 0x0000;

    private readonly HeaderLengthFrameDecoder _innerDecoder;
    private readonly int _headerLength;
    private readonly bool _isFrame4E;

    /// <summary>프레임 종류에 맞는 디코더를 만듭니다.</summary>
    /// <param name="frameType">3E 또는 4E 프레임 종류입니다.</param>
    public MCBinaryFrameDecoder(MCFrameType frameType)
    {
        if (!Enum.IsDefined(typeof(MCFrameType), frameType))
        {
            throw new ArgumentOutOfRangeException(nameof(frameType), frameType, "지원하지 않는 MC 프레임 종류입니다.");
        }

        _isFrame4E = frameType == MCFrameType.Frame4E;
        _headerLength = _isFrame4E ? Header4ELength : Header3ELength;

        // 길이 필드는 헤더의 맨 끝 2바이트이고, 전체 길이 = 헤더 + 길이 필드 값이다.
        _innerDecoder = new HeaderLengthFrameDecoder(
            _headerLength,
            _headerLength - LengthFieldByteCount,
            LengthFieldByteCount,
            isBigEndian: false,
            lengthAdjustment: _headerLength,
            maximumFrameLength: _headerLength + MaxLengthFieldValue);
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < _headerLength)
        {
            frameLength = 0;
            return false;
        }

        if (received.Array is not { } array)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        int offset = received.Offset;
        ushort expectedSubheader = _isFrame4E ? Response4ESubheader : Response3ESubheader;
        ushort subheader = BinaryBuffer.ReadUInt16LittleEndian(array, offset);
        if (subheader != expectedSubheader)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MC 바이너리 응답 서브헤더가 올바르지 않습니다. (수신: 0x{subheader:X4}, 기대: 0x{expectedSubheader:X4})");
        }

        if (_isFrame4E && BinaryBuffer.ReadUInt16LittleEndian(array, offset + ReservedOffset4E) != ReservedWordZero)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "4E 바이너리 응답의 예약 필드가 0x0000이 아닙니다.");
        }

        int lengthField = BinaryBuffer.ReadUInt16LittleEndian(array, offset + _headerLength - LengthFieldByteCount);
        if (lengthField < MinimumResponseBodyBytes)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"MC 바이너리 응답 길이 필드({lengthField})는 최소 {MinimumResponseBodyBytes} 이상이어야 합니다.");
        }

        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
