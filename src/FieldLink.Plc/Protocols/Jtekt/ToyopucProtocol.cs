using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 2PORT-EFR 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class ToyopucProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int BitsPerWord = 16;
    private const int ExtendedBankSize = 0x8000;
    private const byte WriteBitCommandEcho = 0x21;
    private const byte BasicReadWordsCommandEcho = 0x1C;
    private const byte BasicWriteWordsCommandEcho = 0x1D;
    private const byte ExtendedReadWordsCommandEcho = 0x94;
    private const byte ExtendedWriteWordsCommandEcho = 0x95;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly ToyopucOptions _options;
    private readonly ToyopucFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="ToyopucProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신을 수행하는 전송 객체입니다.</param>
    /// <param name="options">TOYOPUC 프로토콜 옵션입니다. null이면 기본 옵션을 사용합니다.</param>
    public ToyopucProtocol(ITransport transport, ToyopucOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new ToyopucOptions();
        _frameDecoder = new ToyopucFrameDecoder();
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = ToyopucAddress.Parse(request.Address, isBit);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            return address.Prg.HasValue
                ? await ReadExtendedBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false)
                : await ReadBasicBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        byte[] buffer = await ReadWordsAsync(address, effectiveRequest, cancellationToken).ConfigureAwait(false);
        return ProtocolExecutionGuard.TrimReadResult(buffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = ToyopucAddress.Parse(request.Address, isBit);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            if (address.Prg.HasValue)
            {
                await WriteExtendedBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteBasicBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBasicBitsAsync(ToyopucAddress startAddress, int bitCount, CancellationToken cancellationToken)
    {
        startAddress.Offset(bitCount - 1);
        byte[] bitValues = new byte[bitCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            for (int i = 0; i < bitCount; i++)
            {
                ToyopucAddress currentAddress = startAddress.Offset(i);
                byte[] requestFrame = ToyopucRequestBuilder.BuildReadBit(currentAddress);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                bitValues[i] = ToyopucResponseParser.ParseReadBit(responseFrame);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return bitValues;
    }

    private async Task<byte[]> ReadExtendedBitsAsync(ToyopucAddress startAddress, int bitCount, CancellationToken cancellationToken)
    {
        int totalWords = CalculateBitSpanWords(startAddress.BitOffset, bitCount);
        startAddress.OffsetWords(totalWords - 1);
        byte[] wordBytes = await ReadWordRangeAsync(startAddress, totalWords, cancellationToken).ConfigureAwait(false);
        return ExtractBitsFromWords(wordBytes, startAddress.BitOffset, bitCount);
    }

    private async Task<byte[]> ReadWordsAsync(ToyopucAddress startAddress, PlcReadRequest effectiveRequest, CancellationToken cancellationToken)
    {
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        List<WordChunk> chunks = PlanWordChunks(startAddress, effectiveRequest.Count, maxElementsPerChunk, wordsPerElement);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (WordChunk chunk in chunks)
            {
                byte[] chunkData = await ExchangeReadWordsAsync(session, chunk.Address, chunk.WordCount).ConfigureAwait(false);
                int destinationOffset = chunk.WordOffset * BytesPerWord;
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadWordRangeAsync(ToyopucAddress startAddress, int totalWords, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        byte[] wordBuffer = new byte[totalWords * BytesPerWord];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ToyopucAddress chunkAddress = startAddress.OffsetWords(chunk.ElementOffset);
                ushort wordCount = checked((ushort)chunk.ElementCount);
                byte[] chunkData = await ExchangeReadWordsAsync(session, chunkAddress, wordCount).ConfigureAwait(false);

                int destinationOffset = chunk.ElementOffset * BytesPerWord;
                Array.Copy(chunkData, 0, wordBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return wordBuffer;
    }

    private async Task<byte[]> ExchangeReadWordsAsync(ITransportSession session, ToyopucAddress address, ushort wordCount)
    {
        byte[] requestFrame = ToyopucRequestBuilder.BuildReadWords(address, wordCount);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        byte expectedCommand = address.Prg.HasValue ? ExtendedReadWordsCommandEcho : BasicReadWordsCommandEcho;
        return ToyopucResponseParser.ParseReadWords(responseFrame, expectedCommand, wordCount);
    }

    private async Task WriteBasicBitsAsync(ToyopucAddress startAddress, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        startAddress.Offset(request.Count - 1);
        byte[] bitData = request.GetData();
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                for (int i = 0; i < request.Count; i++)
                {
                    ToyopucAddress currentAddress = startAddress.Offset(i);
                    bool bitValue = bitData[i] != 0;
                    byte[] requestFrame = ToyopucRequestBuilder.BuildWriteBit(currentAddress, bitValue);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    ToyopucResponseParser.VerifyWriteResponse(responseFrame, WriteBitCommandEcho);
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

    // TOYOPUC 0x21 단일 비트 쓰기 명령에는 PRG 필드가 없으므로 확장 영역 비트 쓰기는 0x94/0x95 Read-Modify-Write로 처리합니다.
    private async Task WriteExtendedBitsAsync(ToyopucAddress startAddress, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalWords = CalculateBitSpanWords(startAddress.BitOffset, request.Count);
        startAddress.OffsetWords(totalWords - 1);
        int maxWordsPerChunk = Math.Min(_options.MaxReadWords, _options.MaxWriteWords);
        List<RequestChunker.Chunk> wordChunks = RequestChunker.Plan(totalWords, maxWordsPerChunk);
        byte[] bitData = request.GetData();
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk wordChunk in wordChunks)
                {
                    int writtenInChunk = await ModifyAndWriteExtendedWordChunkAsync(
                        session,
                        startAddress,
                        wordChunk,
                        bitData).ConfigureAwait(false);
                    completedElements += writtenInChunk;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task<int> ModifyAndWriteExtendedWordChunkAsync(
        ITransportSession session,
        ToyopucAddress startAddress,
        RequestChunker.Chunk wordChunk,
        byte[] bitData)
    {
        ToyopucAddress chunkWordAddress = startAddress.OffsetWords(wordChunk.ElementOffset);
        ushort wordCount = checked((ushort)wordChunk.ElementCount);
        byte[] wordBytes = await ExchangeReadWordsAsync(session, chunkWordAddress, wordCount).ConfigureAwait(false);

        int modifiedBitCount = ApplyBitsToWordChunk(
            wordBytes,
            wordChunk.ElementOffset,
            startAddress.BitOffset,
            bitData);

        byte[] writeFrame = ToyopucRequestBuilder.BuildWriteWords(chunkWordAddress, wordCount, wordBytes);
        byte[] writeResponse = await session.ExchangeAsync(writeFrame, _frameDecoder).ConfigureAwait(false);
        ToyopucResponseParser.VerifyWriteResponse(writeResponse, ExtendedWriteWordsCommandEcho);

        return modifiedBitCount;
    }

    private async Task WriteWordsAsync(ToyopucAddress startAddress, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out int originalElementsPerUnit);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
        List<WordChunk> chunks = PlanWordChunks(startAddress, effectiveRequest.Count, maxElementsPerChunk, wordsPerElement);
        byte[] fullWriteBytes = effectiveRequest.GetData();
        int completedWords = 0;
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (WordChunk chunk in chunks)
                {
                    await WriteSingleWordChunkAsync(session, chunk, fullWriteBytes).ConfigureAwait(false);
                    completedWords += chunk.WordCount;
                    completedElements = CalculateCompletedElements(
                        completedWords,
                        wordsPerElement,
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

    private async Task WriteSingleWordChunkAsync(ITransportSession session, WordChunk chunk, byte[] fullWriteBytes)
    {
        int byteLength = chunk.WordCount * BytesPerWord;
        byte[] chunkData = new byte[byteLength];
        Buffer.BlockCopy(fullWriteBytes, chunk.WordOffset * BytesPerWord, chunkData, 0, byteLength);

        byte[] requestFrame = ToyopucRequestBuilder.BuildWriteWords(chunk.Address, chunk.WordCount, chunkData);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        byte expectedCommand = chunk.Address.Prg.HasValue ? ExtendedWriteWordsCommandEcho : BasicWriteWordsCommandEcho;
        ToyopucResponseParser.VerifyWriteResponse(responseFrame, expectedCommand);
    }

    private static int CalculateCompletedElements(
        int completedWords,
        int wordsPerElement,
        int originalElementsPerUnit,
        int totalOriginalCount)
    {
        if (originalElementsPerUnit > 1)
        {
            return Math.Min(totalOriginalCount, completedWords * originalElementsPerUnit);
        }

        return completedWords / wordsPerElement;
    }

    private static int CalculateBitSpanWords(byte startBitOffset, int bitCount)
    {
        long totalWordsLong = ((long)startBitOffset + bitCount + BitsPerWord - 1) / BitsPerWord;
        if (totalWordsLong > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "요청 비트 범위가 허용 상한을 초과합니다.");
        }

        return (int)totalWordsLong;
    }

    private static List<WordChunk> PlanWordChunks(
        ToyopucAddress startAddress,
        int elementCount,
        int maxElementsPerChunk,
        int wordsPerElement)
    {
        long totalWordsLong = (long)elementCount * wordsPerElement;
        if (totalWordsLong - 1 > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "요청 워드 범위가 허용 상한을 초과합니다.");
        }

        int totalWords = (int)totalWordsLong;
        startAddress.Offset(totalWords - 1);

        int alignedMaxWords = checked(maxElementsPerChunk * wordsPerElement);
        var chunks = new List<WordChunk>();
        int currentWordOffset = 0;

        while (currentWordOffset < totalWords)
        {
            ToyopucAddress chunkAddress = startAddress.Offset(currentWordOffset);
            int remainingWords = totalWords - currentWordOffset;
            int chunkWordLimit = alignedMaxWords;

            if (chunkAddress.Device == "EB")
            {
                int wordsLeftInBank = ExtendedBankSize - chunkAddress.WireAddress;
                chunkWordLimit = Math.Min(chunkWordLimit, wordsLeftInBank);
            }

            ushort wordCount = checked((ushort)Math.Min(remainingWords, chunkWordLimit));
            chunks.Add(new WordChunk(chunkAddress, currentWordOffset, wordCount));
            currentWordOffset += wordCount;
        }

        return chunks;
    }

    private static byte[] ExtractBitsFromWords(byte[] wordBytes, byte startBitOffset, int bitCount)
    {
        byte[] extractedBits = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            int absoluteBitIndex = startBitOffset + i;
            int wordByteOffset = (absoluteBitIndex / BitsPerWord) * BytesPerWord;
            int bitPositionInWord = absoluteBitIndex % BitsPerWord;

            ushort wordValue = BinaryBuffer.ReadUInt16LittleEndian(wordBytes, wordByteOffset);
            int bitMask = 1 << bitPositionInWord;
            extractedBits[i] = (wordValue & bitMask) != 0 ? (byte)1 : (byte)0;
        }

        return extractedBits;
    }

    private static int ApplyBitsToWordChunk(
        byte[] wordBytes,
        int chunkWordOffset,
        byte startBitOffset,
        byte[] bitData)
    {
        int chunkStartBit = chunkWordOffset * BitsPerWord;
        int chunkEndBitExclusive = chunkStartBit + ((wordBytes.Length / BytesPerWord) * BitsPerWord);
        int modifiedCount = 0;

        for (int i = 0; i < bitData.Length; i++)
        {
            int absoluteBitIndex = startBitOffset + i;
            if (absoluteBitIndex < chunkStartBit || absoluteBitIndex >= chunkEndBitExclusive)
            {
                continue;
            }

            int localBitIndex = absoluteBitIndex - chunkStartBit;
            int wordByteOffset = (localBitIndex / BitsPerWord) * BytesPerWord;
            int bitPositionInWord = localBitIndex % BitsPerWord;

            ushort wordValue = BinaryBuffer.ReadUInt16LittleEndian(wordBytes, wordByteOffset);
            ushort bitMask = (ushort)(1 << bitPositionInWord);
            ushort updatedWord = bitData[i] != 0
                ? (ushort)(wordValue | bitMask)
                : (ushort)(wordValue & ~bitMask);

            BinaryBuffer.WriteUInt16LittleEndian(wordBytes, wordByteOffset, updatedWord);
            modifiedCount++;
        }

        return modifiedCount;
    }

    private readonly struct WordChunk
    {
        public WordChunk(ToyopucAddress address, int wordOffset, ushort wordCount)
        {
            Address = address;
            WordOffset = wordOffset;
            WordCount = wordCount;
        }

        public ToyopucAddress Address { get; }

        public int WordOffset { get; }

        public ushort WordCount { get; }
    }
}
