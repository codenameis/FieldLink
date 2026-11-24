using System;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>TPKT 헤더의 길이 필드(빅엔디언, 헤더 포함 전체 길이)로 프레임 경계를 판정하는 디코더입니다. (사양서 §1)</summary>
public sealed class S7CommFrameDecoder : IFrameDecoder
{
    private const int TpktHeaderLength = 4;
    private const int VersionOffset = 0;
    private const int ReservedOffset = 1;
    private const int LengthFieldOffset = 2;
    private const int LengthFieldByteCount = 2;
    private const int NoLengthAdjustment = 0;
    private const int MinTpktLength = 7; // 사양서 §1: TPKT(4) + COTP Data(3) 최소 7바이트
    private const int MaxTpktLength = 65535;
    private const byte ExpectedTpktVersion = 0x03;
    private const byte ExpectedTpktReserved = 0x00;
    private const int ByteShift = 8;

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        TpktHeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: true,
        NoLengthAdjustment,
        MaxTpktLength);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < TpktHeaderLength)
        {
            frameLength = 0;
            return false;
        }

        if (received.Array is not { } buffer)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        int offset = received.Offset;
        if (buffer[offset + VersionOffset] != ExpectedTpktVersion)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"TPKT 버전이 0x{ExpectedTpktVersion:X2}이 아닙니다. (수신: 0x{buffer[offset + VersionOffset]:X2})");
        }

        if (buffer[offset + ReservedOffset] != ExpectedTpktReserved)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"TPKT 예약 바이트가 0x{ExpectedTpktReserved:X2}이 아닙니다. (수신: 0x{buffer[offset + ReservedOffset]:X2})");
        }

        int declaredLength = (buffer[offset + LengthFieldOffset] << ByteShift) | buffer[offset + LengthFieldOffset + 1];
        if (declaredLength < MinTpktLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"TPKT 전체 길이({declaredLength})는 최소 {MinTpktLength}바이트 이상이어야 합니다.");
        }

        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
