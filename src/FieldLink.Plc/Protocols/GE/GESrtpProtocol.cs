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
    private const int BitsPerByte = 8;
    private const int BytesPerWord = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly GESrtpOptions _options;
    private readonly GESrtpFrameDecoder _frameDecoder;
    private readonly object _sessionLock = new();

    private long? _initializedConnectionId;
    private int _sequenceCounter;

    /// <summary>전송 및 옵션으로 <see cref="GESrtpProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 프레임을 주고받는 전송 구현체입니다.</param>
    /// <param name="options">최대 분할 크기 등의 프로토콜 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/>가 null일 때.</exception>
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
        await _transport.RunExclusiveAsync(
            async session =>
            {
                await SendSessionHandshakeAsync(session).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
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
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = address.IsWord
            ? ProtocolExecutionGuard.NormalizeWordReadRequest(request)
            : request;

        int elementByteSize = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = CalculateUnitsPerElement(address, elementByteSize);
        int maxElements = CalculateMaxReadElements(address, elementByteSize, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElements);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(
            async session =>
            {
                await EnsureSessionInitializedAsync(session).ConfigureAwait(false);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    byte[] chunkData = await ReadChunkAsync(session, address, chunk, elementByteSize).ConfigureAwait(false);
                    int destinationOffset = chunk.ElementOffset * elementByteSize;
                    Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        return ProtocolExecutionGuard.TrimReadResult(combinedBuffer, requestedByteCount);
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
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        PlcWriteRequest effectiveRequest = address.IsWord
            ? ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit)
            : request;

        int elementByteSize = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = CalculateUnitsPerElement(address, elementByteSize);
        int maxElements = CalculateMaxWriteElements(address, elementByteSize, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(
                async session =>
                {
                    await EnsureSessionInitializedAsync(session).ConfigureAwait(false);

                    foreach (RequestChunker.Chunk chunk in chunks)
                    {
                        await WriteChunkAsync(session, address, chunk, effectiveRequest).ConfigureAwait(false);
                        completedElements = ProtocolExecutionGuard.AdvanceCompletedCount(
                            completedElements,
                            chunk.ElementCount,
                            originalElementsPerUnit,
                            totalOriginalCount);
                    }

                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private static int CalculateUnitsPerElement(GESrtpAddress address, int elementByteSize)
    {
        if (address.IsBit)
        {
            return 1;
        }

        if (address.IsWord)
        {
            return Math.Max(1, elementByteSize / BytesPerWord);
        }

        return Math.Max(1, elementByteSize);
    }

    private static byte[] UnpackReadBits(byte[] packedBytes, int bitOffsetInByte, int bitCount)
    {
        bool[] unpackedFlags = BitPacking.Unpack(packedBytes, bitOffsetInByte + bitCount);
        byte[] elementBytes = new byte[bitCount];

        for (int i = 0; i < bitCount; i++)
        {
            elementBytes[i] = unpackedFlags[bitOffsetInByte + i] ? (byte)1 : (byte)0;
        }

        return elementBytes;
    }

    private static byte[] PackWriteBits(ushort startWireAddress, byte[] bitElements)
    {
        // 사양서 §1.6: 오프셋 (0-based 시작주소 % 8)부터 쓰기 비트를 배치하고 LSB 우선으로 8비트씩 패킹한다.
        int bitOffsetInByte = startWireAddress % BitsPerByte;
        int totalBitSpan = bitOffsetInByte + bitElements.Length;
        int packedByteCount = (totalBitSpan + (BitsPerByte - 1)) / BitsPerByte;
        byte[] packedBytes = new byte[packedByteCount];

        for (int i = 0; i < bitElements.Length; i++)
        {
            if (bitElements[i] != 0)
            {
                int targetBitPosition = bitOffsetInByte + i;
                int byteIndex = targetBitPosition / BitsPerByte;
                int bitShift = targetBitPosition % BitsPerByte;
                packedBytes[byteIndex] |= (byte)(1 << bitShift);
            }
        }

        return packedBytes;
    }

    private int CalculateMaxReadElements(GESrtpAddress address, int elementByteSize, int unitsPerElement)
    {
        if (address.IsWord)
        {
            int effectiveMaxWords = Math.Min(_options.MaxWordsPerRequest, Math.Max(1, _options.MaxReadBytes / BytesPerWord));
            return RequestChunker.CalculateElementLimit(effectiveMaxWords, unitsPerElement);
        }

        if (address.IsBit)
        {
            return Math.Max(1, (_options.MaxReadBytes * BitsPerByte) - (BitsPerByte - 1));
        }

        return Math.Max(1, _options.MaxReadBytes / elementByteSize);
    }

    private int CalculateMaxWriteElements(GESrtpAddress address, int elementByteSize, int unitsPerElement)
    {
        if (address.IsWord)
        {
            int effectiveMaxWords = Math.Min(_options.MaxWordsPerRequest, Math.Max(1, _options.MaxWriteBytes / BytesPerWord));
            return RequestChunker.CalculateElementLimit(effectiveMaxWords, unitsPerElement);
        }

        if (address.IsBit)
        {
            return Math.Max(1, (_options.MaxWriteBytes * BitsPerByte) - (BitsPerByte - 1));
        }

        return Math.Max(1, _options.MaxWriteBytes / elementByteSize);
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        GESrtpAddress baseAddress,
        RequestChunker.Chunk chunk,
        int elementByteSize)
    {
        int unitsPerElement = CalculateUnitsPerElement(baseAddress, elementByteSize);
        GESrtpAddress chunkAddress = baseAddress.Offset(chunk.ElementOffset * unitsPerElement);
        ushort wireQuantity = (ushort)(chunk.ElementCount * unitsPerElement);
        ushort sequenceId = NextSequenceId();

        byte[] requestFrame = GESrtpRequestBuilder.BuildReadRequest(sequenceId, chunkAddress, wireQuantity);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

        if (chunkAddress.IsBit)
        {
            int bitOffsetInByte = chunkAddress.WireAddress % BitsPerByte;
            int totalBitSpan = bitOffsetInByte + chunk.ElementCount;
            int expectedPackedBytes = (totalBitSpan + (BitsPerByte - 1)) / BitsPerByte;
            byte[] packedBytes = GESrtpResponseParser.ParseReadResponse(responseFrame, sequenceId, expectedPackedBytes);
            return UnpackReadBits(packedBytes, bitOffsetInByte, chunk.ElementCount);
        }

        int expectedByteCount = chunk.ElementCount * elementByteSize;
        return GESrtpResponseParser.ParseReadResponse(responseFrame, sequenceId, expectedByteCount);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        GESrtpAddress baseAddress,
        RequestChunker.Chunk chunk,
        PlcWriteRequest request)
    {
        int unitsPerElement = CalculateUnitsPerElement(baseAddress, request.DataType.GetElementSize());
        GESrtpAddress chunkAddress = baseAddress.Offset(chunk.ElementOffset * unitsPerElement);
        ushort wireQuantity = (ushort)(chunk.ElementCount * unitsPerElement);
        byte[] rawSlice = request.GetData(chunk.ElementOffset, chunk.ElementCount);
        byte[] payloadBytes = chunkAddress.IsBit
            ? PackWriteBits(chunkAddress.WireAddress, rawSlice)
            : rawSlice;

        ushort sequenceId = NextSequenceId();
        byte[] requestFrame = GESrtpRequestBuilder.BuildWriteRequest(sequenceId, chunkAddress, wireQuantity, payloadBytes);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        GESrtpResponseParser.VerifyWriteResponse(responseFrame, sequenceId);
    }

    private async Task EnsureSessionInitializedAsync(ITransportSession session)
    {
        lock (_sessionLock)
        {
            if (_initializedConnectionId == session.ConnectionId)
            {
                return;
            }
        }

        await SendSessionHandshakeAsync(session).ConfigureAwait(false);
    }

    private async Task SendSessionHandshakeAsync(ITransportSession session)
    {
        byte[] initRequest = GESrtpRequestBuilder.BuildInitSession();
        byte[] initResponse = await session.ExchangeAsync(initRequest, _frameDecoder).ConfigureAwait(false);
        GESrtpResponseParser.ValidateInitResponse(initResponse);

        lock (_sessionLock)
        {
            _initializedConnectionId = session.ConnectionId;
        }
    }

    private ushort NextSequenceId()
    {
        int next = Interlocked.Increment(ref _sequenceCounter);
        return (ushort)(next & 0xFFFF);
    }
}
