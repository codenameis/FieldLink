using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 프로토콜 드라이버입니다. (사양서 Part A)</summary>
public sealed class KeyenceHostLinkProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly KeyenceHostLinkOptions _options;
    private readonly KeyenceHostLinkFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="KeyenceHostLinkProtocol"/>을 초기화합니다.</summary>
    public KeyenceHostLinkProtocol(ITransport transport, KeyenceHostLinkOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new KeyenceHostLinkOptions();
        _frameDecoder = new KeyenceHostLinkFrameDecoder();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        byte[] requestFrame = KeyenceHostLinkRequestBuilder.BuildConnect(_options.Station);
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
            KeyenceHostLinkResponseParser.VerifyConnectResponse(responseFrame);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        byte[] requestFrame = KeyenceHostLinkRequestBuilder.BuildDisconnect();
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            await session.SendAsync(requestFrame).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = KeyenceHostLinkAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        int totalElements = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalElements, _options.MaxReadElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                KeyenceHostLinkAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = KeyenceHostLinkRequestBuilder.BuildRead(chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                byte[] chunkData = address.IsBit
                    ? KeyenceHostLinkResponseParser.ParseReadBits(responseFrame, count)
                    : KeyenceHostLinkResponseParser.ParseReadWords(responseFrame, count);

                int destOffset = chunk.ElementOffset * (address.IsBit ? 1 : 2);
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

        var address = KeyenceHostLinkAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        int totalElements = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalElements, _options.MaxWriteElements);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                KeyenceHostLinkAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = address.IsBit
                    ? KeyenceHostLinkRequestBuilder.BuildWriteBits(chunkAddress, count, chunkData)
                    : KeyenceHostLinkRequestBuilder.BuildWriteWords(chunkAddress, count, chunkData);

                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                KeyenceHostLinkResponseParser.VerifyWriteResponse(responseFrame);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
