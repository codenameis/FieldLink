using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 로더 프로토콜 드라이버입니다. (사양서 Part C)</summary>
public sealed class FujiElectricSphProtocol : IPlcProtocol
{
    private const int BitsPerWord = 16;
    private const int BitsPerByte = 8;
    private const int BytesPerWord = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly FujiElectricSphOptions _options;
    private readonly FujiElectricSphFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FujiElectricSphProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">SPH 프로토콜 설정입니다.</param>
    public FujiElectricSphProtocol(ITransport transport, FujiElectricSphOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FujiElectricSphOptions();
        _frameDecoder = new FujiElectricSphFrameDecoder();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        FujiElectricSphAddress address = FujiElectricSphAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        FujiElectricSphAddress baseWordAddress = address.ToWordAddress(address.WordAddress);

        byte[] buffer = await ReadWordsCoreAsync(baseWordAddress, effectiveRequest, cancellationToken).ConfigureAwait(false);
        return ProtocolExecutionGuard.TrimReadResult(buffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        FujiElectricSphAddress address = FujiElectricSphAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordsCoreAsync(
        FujiElectricSphAddress baseWordAddress,
        PlcReadRequest effectiveRequest,
        CancellationToken cancellationToken)
    {
        int elementByteSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementByteSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FujiElectricSphAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset * wordsPerElement);
                ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                byte[] chunkData = await ReadSingleWordChunkAsync(session, chunkAddress, wordCount).ConfigureAwait(false);

                int destinationOffset = chunk.ElementOffset * elementByteSize;
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadSingleWordChunkAsync(
        ITransportSession session,
        FujiElectricSphAddress chunkAddress,
        ushort wordCount)
    {
        byte[] requestFrame = FujiElectricSphRequestBuilder.BuildReadWords(
            _options.ConnectionId,
            chunkAddress,
            wordCount);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        return FujiElectricSphResponseParser.ParseReadWords(responseFrame, _options.ConnectionId, wordCount);
    }

    private async Task<byte[]> ReadBitsAsync(
        FujiElectricSphAddress address,
        int bitCount,
        CancellationToken cancellationToken)
    {
        long startBit = ((long)address.WordAddress * BitsPerWord) + address.BitOffset;
        long endBit = startBit + bitCount - 1;
        uint startWord = (uint)(startBit / BitsPerWord);
        uint endWord = (uint)(endBit / BitsPerWord);
        int totalWordCount = (int)(endWord - startWord + 1);

        FujiElectricSphAddress baseWordAddress = address.ToWordAddress(startWord);
        var wordRequest = new PlcReadRequest(address.Original, PlcDataType.UInt16, totalWordCount);
        byte[] wordBytes = await ReadWordsCoreAsync(baseWordAddress, wordRequest, cancellationToken).ConfigureAwait(false);

        return ExtractBitsFromWords(wordBytes, startBit, startWord, bitCount);
    }

    private static byte[] ExtractBitsFromWords(byte[] wordBytes, long startBit, uint startWord, int bitCount)
    {
        byte[] bitResult = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            long absoluteBit = startBit + i;
            int wordIndex = (int)((absoluteBit / BitsPerWord) - startWord);
            int bitInWord = (int)(absoluteBit % BitsPerWord);
            int byteOffset = (wordIndex * BytesPerWord) + (bitInWord / BitsPerByte);
            int bitInByte = bitInWord % BitsPerByte;

            bool isBitSet = (wordBytes[byteOffset] & (1 << bitInByte)) != 0;
            bitResult[i] = isBitSet ? (byte)1 : (byte)0;
        }

        return bitResult;
    }

    private async Task WriteBitsAsync(
        FujiElectricSphAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        long startBit = ((long)address.WordAddress * BitsPerWord) + address.BitOffset;
        long endBit = startBit + request.Count - 1;
        uint startWord = (uint)(startBit / BitsPerWord);
        uint endWord = (uint)(endBit / BitsPerWord);
        int totalWordCount = (int)(endWord - startWord + 1);

        FujiElectricSphAddress baseWordAddress = address.ToWordAddress(startWord);
        byte[] bitValues = request.GetData();
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                byte[] wordBytes = await ReadRawWordsOnSessionAsync(session, baseWordAddress, totalWordCount).ConfigureAwait(false);
                ApplyBitsToWords(wordBytes, startBit, startWord, bitValues);
                List<RequestChunker.Chunk> writeChunks = RequestChunker.Plan(totalWordCount, _options.MaxWriteWords);

                foreach (RequestChunker.Chunk chunk in writeChunks)
                {
                    await WriteModifiedBitWordChunkAsync(session, baseWordAddress, wordBytes, chunk).ConfigureAwait(false);
                    long coveredEndBitExclusive = ((long)startWord + chunk.ElementOffset + chunk.ElementCount) * BitsPerWord;
                    completedElements = (int)Math.Min(request.Count, coveredEndBitExclusive - startBit);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task<byte[]> ReadRawWordsOnSessionAsync(
        ITransportSession session,
        FujiElectricSphAddress baseWordAddress,
        int totalWordCount)
    {
        List<RequestChunker.Chunk> readChunks = RequestChunker.Plan(totalWordCount, _options.MaxReadWords);
        byte[] wordBytes = new byte[totalWordCount * BytesPerWord];

        foreach (RequestChunker.Chunk chunk in readChunks)
        {
            FujiElectricSphAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset);
            ushort wordCount = (ushort)chunk.ElementCount;
            byte[] chunkData = await ReadSingleWordChunkAsync(session, chunkAddress, wordCount).ConfigureAwait(false);
            Array.Copy(chunkData, 0, wordBytes, chunk.ElementOffset * BytesPerWord, chunkData.Length);
        }

        return wordBytes;
    }

    private static void ApplyBitsToWords(byte[] wordBytes, long startBit, uint startWord, byte[] bitValues)
    {
        for (int i = 0; i < bitValues.Length; i++)
        {
            long absoluteBit = startBit + i;
            int wordIndex = (int)((absoluteBit / BitsPerWord) - startWord);
            int bitInWord = (int)(absoluteBit % BitsPerWord);
            int byteOffset = (wordIndex * BytesPerWord) + (bitInWord / BitsPerByte);
            int bitInByte = bitInWord % BitsPerByte;
            byte mask = (byte)(1 << bitInByte);

            if (bitValues[i] != 0)
            {
                wordBytes[byteOffset] = (byte)(wordBytes[byteOffset] | mask);
            }
            else
            {
                wordBytes[byteOffset] = (byte)(wordBytes[byteOffset] & ~mask);
            }
        }
    }

    private async Task WriteModifiedBitWordChunkAsync(
        ITransportSession session,
        FujiElectricSphAddress baseWordAddress,
        byte[] wordBytes,
        RequestChunker.Chunk chunk)
    {
        FujiElectricSphAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset);
        ushort wordCount = (ushort)chunk.ElementCount;
        byte[] chunkData = new byte[wordCount * BytesPerWord];
        Buffer.BlockCopy(wordBytes, chunk.ElementOffset * BytesPerWord, chunkData, 0, chunkData.Length);

        await WriteSingleWordChunkAsync(session, chunkAddress, wordCount, chunkData).ConfigureAwait(false);
    }

    private async Task WriteWordsAsync(
        FujiElectricSphAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out int originalElementsPerUnit);
        int elementByteSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementByteSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        FujiElectricSphAddress baseWordAddress = address.ToWordAddress(address.WordAddress);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    FujiElectricSphAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteSingleWordChunkAsync(session, chunkAddress, wordCount, chunkData).ConfigureAwait(false);
                    completedElements = ProtocolExecutionGuard.AdvanceCompletedCount(
                        completedElements,
                        chunk.ElementCount,
                        originalElementsPerUnit,
                        totalOriginalCount);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task WriteSingleWordChunkAsync(
        ITransportSession session,
        FujiElectricSphAddress chunkAddress,
        ushort wordCount,
        byte[] chunkData)
    {
        byte[] requestFrame = FujiElectricSphRequestBuilder.BuildWriteWords(
            _options.ConnectionId,
            chunkAddress,
            wordCount,
            chunkData);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        FujiElectricSphResponseParser.VerifyWriteResponse(responseFrame, _options.ConnectionId);
    }
}
