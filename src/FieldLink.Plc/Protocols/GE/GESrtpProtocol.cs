using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class GESrtpProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly GESrtpOptions _options;
    private readonly GESrtpFrameDecoder _frameDecoder;
    private int _sequenceCounter;

    /// <summary>전송 및 옵션으로 <see cref="GESrtpProtocol"/>을 초기화합니다.</summary>
    public GESrtpProtocol(ITransport transport, GESrtpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new GESrtpOptions();
        _frameDecoder = new GESrtpFrameDecoder();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        byte[] initReq = GESrtpRequestBuilder.BuildInitSession();
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            byte[] initRes = await session.ExchangeAsync(initReq, _frameDecoder).ConfigureAwait(false);
            GESrtpResponseParser.ValidateInitResponse(initRes);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = GESrtpAddress.Parse(request.Address, isBitAccess);

        int elementSize = request.DataType.GetElementSize();
        int maxElements = address.IsWord
            ? Math.Max(1, _options.MaxReadBytes / 2)
            : Math.Max(1, _options.MaxReadBytes / elementSize);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                GESrtpAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort seq = NextSequenceId();

                byte[] requestFrame = GESrtpRequestBuilder.BuildReadRequest(seq, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                int expectedBytes = chunk.ElementCount * elementSize;
                byte[] data = GESrtpResponseParser.ParseReadResponse(responseFrame, seq, expectedBytes);

                int destOffset = chunk.ElementOffset * elementSize;
                Array.Copy(data, 0, combinedBuffer, destOffset, data.Length);
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

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = GESrtpAddress.Parse(request.Address, isBitAccess);

        int elementSize = request.DataType.GetElementSize();
        int maxElements = address.IsWord
            ? Math.Max(1, _options.MaxWriteBytes / 2)
            : Math.Max(1, _options.MaxWriteBytes / elementSize);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                GESrtpAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
                ushort seq = NextSequenceId();

                byte[] requestFrame = GESrtpRequestBuilder.BuildWriteRequest(seq, chunkAddress, count, chunkData);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                GESrtpResponseParser.VerifyWriteResponse(responseFrame, seq);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private ushort NextSequenceId()
    {
        int next = Interlocked.Increment(ref _sequenceCounter);
        return (ushort)(next & 0x7FFF);
    }
}
