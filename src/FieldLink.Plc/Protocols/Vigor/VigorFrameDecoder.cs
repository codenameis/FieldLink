using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 DLE 이스케이프 기반 프레임 경계 검출기입니다. (사양서 §1.1, §2.1)</summary>
public sealed class VigorFrameDecoder : IFrameDecoder
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;
    private const int FrameHeaderLength = 2;
    private const int FrameTrailerLength = 2;
    private const int ChecksumHexLength = 2;
    private const int TailAfterEtxMarkerLength = FrameTrailerLength + ChecksumHexLength;
    private const int MaximumFrameLength = 2048;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        frameLength = 0;
        if (received.Array is not { } array || received.Count < FrameHeaderLength)
        {
            return false;
        }

        int offset = received.Offset;
        int count = received.Count;

        if (array[offset] != Dle || array[offset + 1] != Stx)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "VIGOR 프레임이 DLE STX(0x10 0x02)로 시작하지 않습니다.");
        }

        int i = offset + FrameHeaderLength;
        int end = offset + count;
        while (i < end - 1)
        {
            if (array[i] == Dle)
            {
                if (array[i + 1] == Dle)
                {
                    i += FrameHeaderLength;
                    continue;
                }

                if (array[i + 1] == Etx)
                {
                    int totalLength = (i + TailAfterEtxMarkerLength) - offset;
                    if (totalLength > MaximumFrameLength)
                    {
                        throw new TransportException(
                            TransportFailure.InvalidFrame,
                            $"프레임 길이({totalLength})가 최대 허용 길이({MaximumFrameLength})를 초과했습니다.");
                    }

                    if (count >= totalLength)
                    {
                        frameLength = totalLength;
                        return true;
                    }

                    return false;
                }

                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    $"VIGOR 본문 내 이스케이프되지 않은 DLE(0x10)가 감지되었습니다 (다음 바이트: 0x{array[i + 1]:X2}).");
            }

            i++;
        }

        if (count >= MaximumFrameLength)
        {
            throw new TransportException(TransportFailure.InvalidFrame, $"프레임이 최대 허용 길이({MaximumFrameLength})를 초과했습니다.");
        }

        return false;
    }
}
