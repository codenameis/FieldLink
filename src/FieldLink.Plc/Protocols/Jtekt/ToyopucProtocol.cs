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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly ToyopucOptions _options;
    private readonly ToyopucFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="ToyopucProtocol"/>을 초기화합니다.</summary>
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

        if (isBit && request.Count == 1 && !address.Prg.HasValue)
        {
            byte[] requestFrame = ToyopucRequestBuilder.BuildReadBit(address);
            byte bitVal = await _transport.RunExclusiveAsync<byte>(async session =>
            {
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                return ToyopucResponseParser.ParseReadBit(responseFrame);
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
                ToyopucAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = ToyopucRequestBuilder.BuildReadWords(chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte expectedCmd = chunkAddress.Prg.HasValue ? (byte)0x94 : (byte)0x1C;
                byte[] chunkData = ToyopucResponseParser.ParseReadWords(responseFrame, expectedCmd, count);

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
        var address = ToyopucAddress.Parse(request.Address, isBit);

        if (isBit && request.Count == 1 && !address.Prg.HasValue)
        {
            bool value = request.GetData()[0] != 0;
            byte[] requestFrame = ToyopucRequestBuilder.BuildWriteBit(address, value);
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                ToyopucResponseParser.VerifyWriteResponse(responseFrame, expectedCommand: 0x21);
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
                ToyopucAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);

                byte[] requestFrame = ToyopucRequestBuilder.BuildWriteWords(chunkAddress, count, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte expectedCmd = chunkAddress.Prg.HasValue ? (byte)0x95 : (byte)0x1D;
                ToyopucResponseParser.VerifyWriteResponse(responseFrame, expectedCmd);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
