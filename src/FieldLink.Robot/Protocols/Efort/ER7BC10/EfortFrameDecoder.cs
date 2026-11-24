using System;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT 바이너리 상태 프레임의 헤더 길이를 해석해 프레임 경계를 판정하는 디코더입니다.</summary>
public sealed class EfortFrameDecoder : IFrameDecoder
{
    private const int DefaultMaximumFrameLength = 65536;

    private readonly IEfortPacketLayout _layout;
    private readonly int _maximumFrameLength;

    /// <summary>프로토콜 버전과 최대 프레임 길이로 디코더를 초기화합니다.</summary>
    /// <param name="version">v2 정렬 또는 v1 비정렬 버전입니다.</param>
    /// <param name="maximumFrameLength">허용할 최대 프레임 바이트 수입니다.</param>
    public EfortFrameDecoder(EfortProtocolVersion version = EfortProtocolVersion.V2Aligned, int maximumFrameLength = DefaultMaximumFrameLength)
    {
        _layout = version == EfortProtocolVersion.V2Aligned
            ? EfortV2AlignedLayout.Instance
            : EfortV1UnalignedLayout.Instance;

        if (maximumFrameLength < _layout.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameLength), maximumFrameLength, "최대 프레임 길이는 헤더 길이 이상이어야 합니다.");
        }

        _maximumFrameLength = maximumFrameLength;
    }

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < _layout.HeaderLength)
        {
            frameLength = 0;
            return false;
        }

        short declaredLength = RobotBinaryBuffer.ReadInt16LittleEndian(received.Array!, received.Offset + _layout.LengthFieldOffset);
        // 사양서 02 §3, §7: 전체 길이가 헤더보다 작으면 본문 길이 0으로 처리해 후속 최소 길이 검사에서 실패시킴
        int expectedLength = declaredLength < _layout.HeaderLength ? _layout.HeaderLength : declaredLength;

        if (expectedLength > _maximumFrameLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"EFORT 프레임 길이({expectedLength} 바이트)가 최대 허용 범위({_maximumFrameLength})를 초과했습니다.");
        }

        if (received.Count < expectedLength)
        {
            frameLength = 0;
            return false;
        }

        frameLength = expectedLength;
        return true;
    }
}
