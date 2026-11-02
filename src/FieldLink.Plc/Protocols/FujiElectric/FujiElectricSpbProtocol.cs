using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 로더 ASCII 프로토콜 드라이버입니다. (사양서 Part A)</summary>
public sealed class FujiElectricSpbProtocol : IPlcProtocol
{
    private const int BitsPerWord = 16;
    private const int BitsPerByte = 8;
    private const int BytesPerWord = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly FujiElectricSpbOptions _options;
    private readonly FujiElectricSpbFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FujiElectricSpbProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">SPB 프로토콜 설정입니다.</param>
    public FujiElectricSpbProtocol(ITransport transport, FujiElectricSpbOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FujiElectricSpbOptions();
        _frameDecoder = new FujiElectricSpbFrameDecoder();
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

        FujiElectricSpbAddress address = FujiElectricSpbAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        FujiElectricSpbAddress baseWordAddress = address.ToWordAddress(address.WordAddress);

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

        FujiElectricSpbAddress address = FujiElectricSpbAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordsCoreAsync(
        FujiElectricSpbAddress baseWordAddress,
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
                FujiElectricSpbAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset * wordsPerElement);
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
        FujiElectricSpbAddress chunkAddress,
        ushort wordCount)
    {
        byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildReadWords(
            _options.Station,
            chunkAddress,
            wordCount,
            _options.IncludeLrc);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        return FujiElectricSpbResponseParser.ParseReadWords(responseFrame, _options.Station, wordCount);
    }

    private async Task<byte[]> ReadBitsAsync(
        FujiElectricSpbAddress address,
        int bitCount,
        CancellationToken cancellationToken)
    {
        int startBit = (address.WordAddress * BitsPerWord) + address.BitOffset;
        int endBit = startBit + bitCount - 1;
        ushort startWord = (ushort)(startBit / BitsPerWord);
        ushort endWord = (ushort)(endBit / BitsPerWord);
        int totalWordCount = endWord - startWord + 1;

        FujiElectricSpbAddress baseWordAddress = address.ToWordAddress(startWord);
        var wordRequest = new PlcReadRequest(address.Original, PlcDataType.UInt16, totalWordCount);
        byte[] wordBytes = await ReadWordsCoreAsync(baseWordAddress, wordRequest, cancellationToken).ConfigureAwait(false);

        return ExtractBitsFromWords(wordBytes, startBit, startWord, bitCount);
    }

    private static byte[] ExtractBitsFromWords(byte[] wordBytes, int startBit, ushort startWord, int bitCount)
    {
        byte[] bitResult = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            int absoluteBit = startBit + i;
            int wordIndex = (absoluteBit / BitsPerWord) - startWord;
            int bitInWord = absoluteBit % BitsPerWord;
            int byteOffset = (wordIndex * BytesPerWord) + (bitInWord / BitsPerByte);
            int bitInByte = bitInWord % BitsPerByte;

            bool isBitSet = (wordBytes[byteOffset] & (1 << bitInByte)) != 0;
            bitResult[i] = isBitSet ? (byte)1 : (byte)0;
        }

        return bitResult;
    }

    private async Task WriteBitsAsync(
        FujiElectricSpbAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        FujiElectricSpbAddress baseBitAddress = address.ToBitAddress();
        byte[] bitValues = request.GetData();
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                for (int i = 0; i < request.Count; i++)
                {
                    FujiElectricSpbAddress targetBitAddress = baseBitAddress.Offset(i);
                    bool bitValue = bitValues[i] != 0;
                    await WriteSingleBitPointAsync(session, targetBitAddress, bitValue).ConfigureAwait(false);
                    completedElements++;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task WriteSingleBitPointAsync(
        ITransportSession session,
        FujiElectricSpbAddress targetBitAddress,
        bool bitValue)
    {
        byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildWriteBit(
            _options.Station,
            targetBitAddress,
            bitValue,
            _options.IncludeLrc);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        FujiElectricSpbResponseParser.VerifyWriteResponse(
            responseFrame,
            _options.Station,
            FujiElectricSpbResponseParser.WriteBitCommandEcho);
    }

    private async Task WriteWordsAsync(
        FujiElectricSpbAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out int originalElementsPerUnit);
        int elementByteSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementByteSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        FujiElectricSpbAddress baseWordAddress = address.ToWordAddress(address.WordAddress);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    FujiElectricSpbAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset * wordsPerElement);
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
        FujiElectricSpbAddress chunkAddress,
        ushort wordCount,
        byte[] chunkData)
    {
        byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildWriteWords(
            _options.Station,
            chunkAddress,
            wordCount,
            chunkData,
            _options.IncludeLrc);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        FujiElectricSpbResponseParser.VerifyWriteResponse(
            responseFrame,
            _options.Station,
            FujiElectricSpbResponseParser.WriteWordsCommandEcho);
    }
}
