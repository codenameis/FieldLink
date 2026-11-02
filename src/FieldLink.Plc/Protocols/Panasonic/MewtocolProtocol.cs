using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class MewtocolProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const string WriteCoilEcho = "WC";
    private const string WriteWordsEcho = "WD";

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly MewtocolOptions _options;
    private readonly MewtocolFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="MewtocolProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신을 수행하는 전송 객체입니다.</param>
    /// <param name="options">MEWTOCOL-COM 프로토콜 옵션입니다. null이면 기본 옵션을 사용합니다.</param>
    public MewtocolProtocol(ITransport transport, MewtocolOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new MewtocolOptions();
        _frameDecoder = new MewtocolFrameDecoder();
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

        var address = MewtocolAddress.Parse(request.Address);
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

        var address = MewtocolAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(MewtocolAddress startAddress, int bitCount, CancellationToken cancellationToken)
    {
        byte[] resultBuffer = new byte[bitCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            for (int i = 0; i < bitCount; i++)
            {
                MewtocolAddress currentAddress = startAddress.Offset(i);
                byte[] requestFrame = MewtocolRequestBuilder.BuildReadSingleBit(_options.Station, currentAddress);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                resultBuffer[i] = MewtocolResponseParser.ParseReadSingleBit(responseFrame, _options.Station);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return resultBuffer;
    }

    private async Task<byte[]> ReadWordsAsync(MewtocolAddress startAddress, PlcReadRequest effectiveRequest, CancellationToken cancellationToken)
    {
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                MewtocolAddress chunkAddress = startAddress.Offset(chunk.ElementOffset * wordsPerElement);
                ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);

                byte[] requestFrame = MewtocolRequestBuilder.BuildReadWords(_options.Station, chunkAddress, wordCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = MewtocolResponseParser.ParseReadWords(responseFrame, _options.Station, wordCount);

                int destinationOffset = chunk.ElementOffset * elementSize;
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task WriteBitsAsync(MewtocolAddress startAddress, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        byte[] bitData = request.GetData();
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                for (int i = 0; i < request.Count; i++)
                {
                    MewtocolAddress currentAddress = startAddress.Offset(i);
                    bool bitValue = bitData[i] != 0;
                    byte[] requestFrame = MewtocolRequestBuilder.BuildWriteSingleBit(_options.Station, currentAddress, bitValue);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, WriteCoilEcho);
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

    private async Task WriteWordsAsync(MewtocolAddress startAddress, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out int originalElementsPerUnit);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    MewtocolAddress chunkAddress = startAddress.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = MewtocolRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, WriteWordsEcho);
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
