using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK FBs/B1 계열 PLC와 통신하는 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class FatekProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly FatekOptions _options;
    private readonly FatekFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FatekProtocol"/> 인스턴스를 초기화합니다.</summary>
    public FatekProtocol(ITransport transport, FatekOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FatekOptions();
        _frameDecoder = new FatekFrameDecoder();
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

        var address = FatekAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int maxElements = address.IsBit ? _options.MaxReadBits : _options.MaxReadWords;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FatekAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = address.IsBit
                    ? FatekRequestBuilder.BuildReadBits(_options.Station, chunkAddress, count)
                    : FatekRequestBuilder.BuildReadWords(_options.Station, chunkAddress, count);

                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                byte[] chunkData = address.IsBit
                    ? FatekResponseParser.ParseReadBits(responseFrame, _options.Station, count)
                    : FatekResponseParser.ParseReadWords(responseFrame, _options.Station, count);

                int destOffset = chunk.ElementOffset * elementSize;
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

        var address = FatekAddress.Parse(request.Address);
        int maxElements = address.IsBit ? _options.MaxWriteBits : _options.MaxWriteWords;
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FatekAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                byte[] requestFrame = address.IsBit
                    ? FatekRequestBuilder.BuildWriteBits(_options.Station, chunkAddress, count, chunkData)
                    : FatekRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, count, chunkData);

                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                string expectedCmd = address.IsBit ? "45" : "47";
                FatekResponseParser.VerifyWriteResponse(responseFrame, _options.Station, expectedCmd);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
