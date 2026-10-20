using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 Computer Link 프로토콜 클라이언트입니다. (사양서 §1~§3)</summary>
public sealed class VigorProtocol : IPlcProtocol
{
    private const int MaxBitReadChunk = 1024;
    private const int MaxWordReadChunk = 64;
    private const int Max32BitWordReadChunk = 32;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly VigorOptions _options;
    private readonly VigorFrameDecoder _frameDecoder = new();

    /// <summary><see cref="VigorProtocol"/> 새 인스턴스를 초기화합니다.</summary>
    public VigorProtocol(ITransport transport, VigorOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new VigorOptions();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public ITransport Transport => _transport;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = VigorAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(VigorAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] packed = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);

                for (int i = 0; i < count; i++)
                {
                    bool bit = (packed[i / 8] & (1 << (i % 8))) != 0;
                    result[chunk.ElementOffset + i] = bit ? (byte)1 : (byte)0;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(VigorAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int bytesPerElement = request.DataType.GetElementSize();
        int wordsPerElement = address.Is32BitWord ? 2 : Math.Max(1, bytesPerElement / 2);
        int totalWords = request.Count * wordsPerElement;
        int maxChunk = address.Is32BitWord ? Max32BitWordReadChunk : MaxWordReadChunk;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, maxChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);

                int destOffset = chunk.ElementOffset * 2;
                Array.Copy(chunkData, 0, combinedBuffer, destOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = VigorAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteBitsAsync(VigorAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int count = request.Count;
        byte[] raw = request.GetData();
        bool[] bools = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bools[i] = raw[i] != 0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(count, MaxBitReadChunk);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                bool[] subBools = new bool[chunk.ElementCount];
                Array.Copy(bools, chunk.ElementOffset, subBools, 0, chunk.ElementCount);

                byte[] requestFrame = VigorRequestBuilder.BuildWriteBitRequest(_options.StationNumber, chunkAddress, subBools);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteWordsAsync(VigorAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.Is32BitWord ? 2 : Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;
        int maxChunk = address.Is32BitWord ? Max32BitWordReadChunk : MaxWordReadChunk;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, maxChunk);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = VigorRequestBuilder.BuildWriteWordRequest(_options.StationNumber, chunkAddress, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
