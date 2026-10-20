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
    private const int MaximumFrameLength = 2048;

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        frameLength = 0;
        if (received.Array is null || received.Count < 2)
        {
            return false;
        }

        byte[] array = received.Array;
        int offset = received.Offset;
        int count = received.Count;

        if (array[offset] != Dle || array[offset + 1] != Stx)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "VIGOR 프레임이 DLE STX(0x10 0x02)로 시작하지 않습니다.");
        }

        int i = offset + 2;
        int end = offset + count;
        while (i < end - 1)
        {
            if (array[i] == Dle)
            {
                if (array[i + 1] == Dle)
                {
                    i += 2;
                    continue;
                }

                if (array[i + 1] == Etx)
                {
                    int totalLen = (i + 2 + 2) - offset;
                    if (count >= totalLen)
                    {
                        frameLength = totalLen;
                        return true;
                    }

                    return false;
                }
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
