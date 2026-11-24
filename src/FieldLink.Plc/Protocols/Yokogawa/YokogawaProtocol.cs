using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 higher-level link service 바이너리 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class YokogawaProtocol : IPlcProtocol
{
    private const byte CommandReadBits = 0x01;
    private const byte CommandWriteBits = 0x02;
    private const byte CommandReadWords = 0x11;
    private const byte CommandWriteWords = 0x12;
    private const int BytesPerWord = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly YokogawaOptions _options;
    private readonly YokogawaFrameDecoder _frameDecoder = new();

    /// <summary><see cref="YokogawaProtocol"/>의 새 인스턴스를 초기화합니다.</summary>
    public YokogawaProtocol(ITransport transport, YokogawaOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YokogawaOptions();
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

        var address = YokogawaAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
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

        var address = YokogawaAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(YokogawaAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, _options.MaxBitsPerRequest);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort bitCount = (ushort)chunk.ElementCount;

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, bitCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] bitBytes = YokogawaResponseParser.ParseReadResponse(responseFrame, CommandReadBits);

                if (bitBytes.Length != bitCount)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"Yokogawa 비트 응답 데이터 길이가 일치하지 않습니다: 기대={bitCount}, 실제={bitBytes.Length}");
                }

                Array.Copy(bitBytes, 0, result, chunk.ElementOffset, bitCount);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(YokogawaAddress address, PlcReadRequest effectiveRequest, CancellationToken cancellationToken)
    {
        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, bytesPerElement / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWordsPerRequest, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                int expectedByteCount = wordCount * BytesPerWord;

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, wordCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = YokogawaResponseParser.ParseReadResponse(responseFrame, CommandReadWords);

                if (chunkData.Length != expectedByteCount)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"Yokogawa 워드 응답 데이터 길이가 일치하지 않습니다: 기대={expectedByteCount}, 실제={chunkData.Length}");
                }

                int bufferByteOffset = chunk.ElementOffset * bytesPerElement;
                Array.Copy(chunkData, 0, combinedBuffer, bufferByteOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task WriteBitsAsync(YokogawaAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalBits = request.Count;
        byte[] rawBytes = request.GetData();
        byte[] bitBytes = new byte[totalBits];
        for (int i = 0; i < totalBits; i++)
        {
            bitBytes[i] = rawBytes[i] != 0 ? (byte)1 : (byte)0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, _options.MaxBitsPerRequest);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                    ushort bitCount = (ushort)chunk.ElementCount;
                    byte[] chunkBits = new byte[bitCount];
                    Array.Copy(bitBytes, chunk.ElementOffset, chunkBits, 0, bitCount);

                    byte[] requestFrame = YokogawaRequestBuilder.BuildWriteBitRequest(_options.CpuNumber, chunkAddress, bitCount, chunkBits);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    YokogawaResponseParser.VerifyWriteResponse(responseFrame, CommandWriteBits);
                    completedElements += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task WriteWordsAsync(YokogawaAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out int originalElementsPerUnit);
        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, bytesPerElement / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWordsPerRequest, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = YokogawaRequestBuilder.BuildWriteWordRequest(_options.CpuNumber, chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    YokogawaResponseParser.VerifyWriteResponse(responseFrame, CommandWriteWords);
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
}
