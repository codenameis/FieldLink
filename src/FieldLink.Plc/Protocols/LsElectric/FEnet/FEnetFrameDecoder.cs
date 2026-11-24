using System;
using System.Text;
using FieldLink.Plc.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 공통 헤더의 길이 필드를 해석하여 프레임 경계를 판정하는 디코더입니다. (사양서 §1, §3-1)</summary>
public sealed class FEnetFrameDecoder : IFrameDecoder
{
    private const int HeaderLength = 20;
    private const int LengthFieldOffset = 16;
    private const int LengthFieldByteCount = 2;
    private const int LengthAdjustment = 20; // 전체 프레임 = 헤더 20바이트 + Length 필드 값
    private const int MinimumInstructionLength = 8;
    private const int MaximumInstructionHeaderLength = 14;
    private const int MaximumFrameSize = HeaderLength + MaximumInstructionHeaderLength + FEnetOptions.ProtocolMaxChunkBytes;
    private static readonly byte[] CompanyId = Encoding.ASCII.GetBytes("LSIS-XGT");

    private readonly HeaderLengthFrameDecoder _innerDecoder = new(
        HeaderLength,
        LengthFieldOffset,
        LengthFieldByteCount,
        isBigEndian: false, // XGT FEnet 길이 필드는 리틀 엔디언
        LengthAdjustment,
        MaximumFrameSize);

    /// <inheritdoc />
    public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
    {
        if (received.Count < HeaderLength)
        {
            frameLength = 0;
            return false;
        }

        if (received.Array is not { } array)
        {
            throw new TransportException(TransportFailure.InvalidFrame, "수신 세그먼트의 내부 배열이 비어 있습니다.");
        }

        int offset = received.Offset;
        for (int index = 0; index < CompanyId.Length; index++)
        {
            if (array[offset + index] != CompanyId[index])
            {
                throw new TransportException(TransportFailure.InvalidFrame, "FEnet 헤더의 Company ID가 'LSIS-XGT'가 아닙니다.");
            }
        }

        int instructionLength = BinaryBuffer.ReadUInt16LittleEndian(array, offset + LengthFieldOffset);
        if (instructionLength < MinimumInstructionLength)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"FEnet 헤더 길이 필드({instructionLength})는 최소 {MinimumInstructionLength} 이상이어야 합니다.");
        }

        return _innerDecoder.TryGetFrameLength(received, out frameLength);
    }
}
