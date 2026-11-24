using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

public sealed partial class FEnetProtocol
{
    private async Task<byte[]> ReadBitsAsync(FEnetAddress address, int bitCount, CancellationToken cancellationToken)
    {
        ValidateBitRange(address, bitCount);

        int firstByte = address.Offset / BitsPerByte;
        int lastByte = (int)(((long)address.Offset + bitCount - 1) / BitsPerByte);
        int totalBytesToRead = lastByte - firstByte + 1;
        List<RequestChunker.Chunk> byteChunks = RequestChunker.Plan(totalBytesToRead, _options.MaxReadBytes);
        byte[] rawBytes = new byte[totalBytesToRead];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetInvokeIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk byteChunk in byteChunks)
            {
                string variableName = address.FormatByteVariableName(firstByte + byteChunk.ElementOffset);
                ushort readByteCount = (ushort)byteChunk.ElementCount;
                byte[] chunkData = await ReadChunkAsync(session, variableName, readByteCount).ConfigureAwait(false);
                Array.Copy(chunkData, 0, rawBytes, byteChunk.ElementOffset, readByteCount);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ExtractBits(rawBytes, address.Offset, firstByte, bitCount);
    }

    private async Task WriteBitsAsync(FEnetAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        ValidateBitRange(address, request.Count);

        int firstByte = address.Offset / BitsPerByte;
        int lastByte = (int)(((long)address.Offset + request.Count - 1) / BitsPerByte);
        int totalBytesToWrite = lastByte - firstByte + 1;
        List<RequestChunker.Chunk> byteChunks = RequestChunker.Plan(totalBytesToWrite, _options.MaxWriteBytes);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetInvokeIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk byteChunk in byteChunks)
                {
                    int bitsWritten = await WriteBitByteChunkAsync(session, address, request, firstByte, byteChunk).ConfigureAwait(false);
                    completedElements += bitsWritten;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task<int> WriteBitByteChunkAsync(
        ITransportSession session,
        FEnetAddress address,
        PlcWriteRequest request,
        int firstByte,
        RequestChunker.Chunk byteChunk)
    {
        int chunkFirstByte = firstByte + byteChunk.ElementOffset;
        int chunkLastByte = chunkFirstByte + byteChunk.ElementCount - 1;
        long chunkFirstBit = Math.Max((long)address.Offset, (long)chunkFirstByte * BitsPerByte);
        long chunkLastBit = Math.Min((long)address.Offset + request.Count - 1, ((long)chunkLastByte * BitsPerByte) + MaxBitInByte);
        int requestBitOffset = (int)(chunkFirstBit - address.Offset);
        int bitsInChunk = (int)(chunkLastBit - chunkFirstBit + 1);

        byte[] writeBuffer = await PrepareBitWriteBufferAsync(
            session,
            address,
            chunkFirstByte,
            chunkLastByte,
            chunkFirstBit,
            chunkLastBit,
            byteChunk.ElementCount).ConfigureAwait(false);

        byte[] bitSlice = request.GetData(requestBitOffset, bitsInChunk);
        ApplyBitsToByteBuffer(writeBuffer, chunkFirstByte, chunkFirstBit, bitSlice);

        await WriteChunkAsync(session, address.FormatByteVariableName(chunkFirstByte), writeBuffer).ConfigureAwait(false);
        return bitsInChunk;
    }

    private async Task<byte[]> PrepareBitWriteBufferAsync(
        ITransportSession session,
        FEnetAddress address,
        int chunkFirstByte,
        int chunkLastByte,
        long chunkFirstBit,
        long chunkLastBit,
        int byteCount)
    {
        byte[] writeBuffer = new byte[byteCount];
        bool needsFirstByteRead = (chunkFirstBit % BitsPerByte) != 0
            || (chunkFirstByte == chunkLastByte && (chunkLastBit % BitsPerByte) != MaxBitInByte);
        bool needsLastByteRead = chunkLastByte != chunkFirstByte && (chunkLastBit % BitsPerByte) != MaxBitInByte;

        if (needsFirstByteRead)
        {
            byte[] firstByteData = await ReadChunkAsync(session, address.FormatByteVariableName(chunkFirstByte), 1).ConfigureAwait(false);
            writeBuffer[0] = firstByteData[0];
        }

        if (needsLastByteRead)
        {
            byte[] lastByteData = await ReadChunkAsync(session, address.FormatByteVariableName(chunkLastByte), 1).ConfigureAwait(false);
            writeBuffer[writeBuffer.Length - 1] = lastByteData[0];
        }

        return writeBuffer;
    }

    private static void ApplyBitsToByteBuffer(byte[] writeBuffer, int chunkFirstByte, long chunkFirstBit, byte[] bitValues)
    {
        for (int index = 0; index < bitValues.Length; index++)
        {
            long bitPosition = chunkFirstBit + index;
            int byteIndex = (int)(bitPosition / BitsPerByte) - chunkFirstByte;
            int bitOffset = (int)(bitPosition % BitsPerByte);
            byte mask = (byte)(1 << bitOffset);

            if (bitValues[index] != 0)
            {
                writeBuffer[byteIndex] |= mask;
            }
            else
            {
                writeBuffer[byteIndex] &= (byte)~mask;
            }
        }
    }

    private static byte[] ExtractBits(byte[] rawBytes, int startBitOffset, int firstByte, int bitCount)
    {
        byte[] bitValues = new byte[bitCount];
        for (int index = 0; index < bitCount; index++)
        {
            long bitPosition = (long)startBitOffset + index;
            int byteIndex = (int)(bitPosition / BitsPerByte) - firstByte;
            int bitOffset = (int)(bitPosition % BitsPerByte);
            bitValues[index] = (rawBytes[byteIndex] & (1 << bitOffset)) != 0 ? BitOnValue : BitOffValue;
        }

        return bitValues;
    }

    private static void ValidateBitRange(FEnetAddress address, int bitCount)
    {
        if ((long)address.Offset + bitCount - 1 > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "요청 비트 범위가 FEnet 32비트 주소 오프셋 한도를 초과합니다.");
        }
    }
}
