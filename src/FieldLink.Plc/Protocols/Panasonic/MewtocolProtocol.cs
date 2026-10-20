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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly MewtocolOptions _options;
    private readonly MewtocolFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="MewtocolProtocol"/>을 초기화합니다.</summary>
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
        if (request.DataType == PlcDataType.Boolean && request.Count == 1)
        {
            byte[] requestFrame = MewtocolRequestBuilder.BuildReadSingleBit(_options.Station, address);
            byte bitVal = await _transport.RunExclusiveAsync<byte>(async session =>
            {
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                return MewtocolResponseParser.ParseReadSingleBit(responseFrame, _options.Station);
            }, cancellationToken).ConfigureAwait(false);

            return [bitVal];
        }

        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                MewtocolAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = MewtocolRequestBuilder.BuildReadWords(_options.Station, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = MewtocolResponseParser.ParseReadWords(responseFrame, _options.Station, count);

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

        var address = MewtocolAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean && request.Count == 1)
        {
            bool bitVal = request.GetData()[0] != 0;
            byte[] requestFrame = MewtocolRequestBuilder.BuildWriteSingleBit(_options.Station, address, bitVal);
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, "WC");
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
                MewtocolAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = MewtocolRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, count, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, "WD");
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
