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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly FujiElectricSphOptions _options;
    private readonly FujiElectricSphFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FujiElectricSphProtocol"/>을 초기화합니다.</summary>
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

        var address = FujiElectricSphAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordsAsync(FujiElectricSphAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FujiElectricSphAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = FujiElectricSphRequestBuilder.BuildReadWords(_options.ConnectionId, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] data = FujiElectricSphResponseParser.ParseReadWords(responseFrame, _options.ConnectionId, count);

                int destOffset = chunk.ElementOffset * 2;
                Array.Copy(data, 0, combinedBuffer, destOffset, data.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadBitsAsync(FujiElectricSphAddress address, int bitCount, CancellationToken cancellationToken)
    {
        int startBit = (int)((address.WordAddress * 16) + address.BitOffset);
        int endBit = startBit + bitCount - 1;
        int startWord = startBit / 16;
        int endWord = endBit / 16;
        ushort totalWords = (ushort)(endWord - startWord + 1);

        FujiElectricSphAddress baseWordAddr = address.Offset(startWord - (int)address.WordAddress);
        var wordRequest = new PlcReadRequest(baseWordAddr.Original, PlcDataType.UInt16, totalWords);
        byte[] wordBytes = await ReadWordsAsync(baseWordAddr, wordRequest, cancellationToken).ConfigureAwait(false);

        byte[] bitResult = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            int absoluteBit = startBit + i;
            int wordIdx = (absoluteBit / 16) - startWord;
            int bitInWord = absoluteBit % 16;
            int byteOffset = (wordIdx * 2) + (bitInWord / 8);
            int bitInByte = bitInWord % 8;

            bool isSet = (wordBytes[byteOffset] & (1 << bitInByte)) != 0;
            bitResult[i] = (byte)(isSet ? 1 : 0);
        }

        return bitResult;
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = FujiElectricSphAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxWriteWords);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FujiElectricSphAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = FujiElectricSphRequestBuilder.BuildWriteWords(_options.ConnectionId, chunkAddress, count, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                FujiElectricSphResponseParser.VerifyWriteResponse(responseFrame, _options.ConnectionId);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
