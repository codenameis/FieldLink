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
    private const int MaxWordReadChunk = 64;
    private const int MaxBitReadChunk = 256;

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
    public ITransport Transport => _transport;

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
        var address = YokogawaAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(YokogawaAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] bitBytes = YokogawaResponseParser.ParseReadResponse(responseFrame, expectedCommand: 0x01);

                Array.Copy(bitBytes, 0, result, chunk.ElementOffset, count);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(YokogawaAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int bytesPerElement = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, bytesPerElement / 2);
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, MaxWordReadChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = YokogawaResponseParser.ParseReadResponse(responseFrame, expectedCommand: 0x11);

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
        var address = YokogawaAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteBitsAsync(YokogawaAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalBits = request.Count;
        byte[] raw = request.GetData();
        byte[] bitBytes = new byte[totalBits];
        for (int i = 0; i < totalBits; i++)
        {
            bitBytes[i] = raw[i] != 0 ? (byte)1 : (byte)0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] subBits = new byte[count];
                Array.Copy(bitBytes, chunk.ElementOffset, subBits, 0, count);

                byte[] requestFrame = YokogawaRequestBuilder.BuildWriteBitRequest(_options.CpuNumber, chunkAddress, count, subBits);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                YokogawaResponseParser.VerifyWriteResponse(responseFrame, expectedCommand: 0x02);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteWordsAsync(YokogawaAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, MaxWordReadChunk);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = YokogawaRequestBuilder.BuildWriteWordRequest(_options.CpuNumber, chunkAddress, count, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                YokogawaResponseParser.VerifyWriteResponse(responseFrame, expectedCommand: 0x12);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
