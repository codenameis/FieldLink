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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly FujiElectricSpbOptions _options;
    private readonly FujiElectricSpbFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FujiElectricSpbProtocol"/>을 초기화합니다.</summary>
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

        var address = FujiElectricSpbAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordsAsync(FujiElectricSpbAddress address, PlcReadRequest request, CancellationToken cancellationToken)
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
                FujiElectricSpbAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildReadWords(_options.Station, chunkAddress, count, _options.IncludeLrc);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] data = FujiElectricSpbResponseParser.ParseReadWords(responseFrame, _options.Station, count);

                int destOffset = chunk.ElementOffset * 2;
                Array.Copy(data, 0, combinedBuffer, destOffset, data.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadBitsAsync(FujiElectricSpbAddress address, int bitCount, CancellationToken cancellationToken)
    {
        int startBit = (address.WordAddress * 16) + address.BitOffset;
        int endBit = startBit + bitCount - 1;
        ushort startWord = (ushort)(startBit / 16);
        ushort endWord = (ushort)(endBit / 16);
        ushort totalWords = (ushort)(endWord - startWord + 1);

        FujiElectricSpbAddress baseWordAddr = FujiElectricSpbAddress.Parse($"{address.AreaCode}{startWord}");
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

        var address = FujiElectricSpbAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean && request.Count == 1)
        {
            bool bitVal = request.GetData()[0] != 0;
            byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildWriteBit(_options.Station, address, bitVal, _options.IncludeLrc);
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                FujiElectricSpbResponseParser.VerifyWriteResponse(responseFrame, _options.Station, "0102");
                return true;
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxWriteWords);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FujiElectricSpbAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = FujiElectricSpbRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, count, chunkData, _options.IncludeLrc);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                FujiElectricSpbResponseParser.VerifyWriteResponse(responseFrame, _options.Station, "0100");
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
