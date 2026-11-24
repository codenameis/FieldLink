using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>TCP 소켓 상에서 실행되는 LS ELECTRIC XGT FEnet 클라이언트 프로토콜 구현체입니다. (사양서 §1, §3)</summary>
public sealed partial class FEnetProtocol : IPlcProtocol
{
    private const int InvokeIdOffset = 14;
    private const int BitsPerByte = 8;
    private const int MaxBitInByte = 7;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;
    private const ushort MaxInvokeId = 0xFFFF;
    private const ushort InitialInvokeId = 0x0001;
    private const int WordSizeBytes = 2;
    private const byte StringPaddingByte = 0x00;
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: WordSizeBytes);

    private readonly ITransport _transport;
    private readonly FEnetOptions _options;
    private readonly FEnetFrameDecoder _frameDecoder = new();
    private readonly object _invokeLock = new();

    private ushort _invokeId;
    private long _lastConnectionId = -1;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public FEnetProtocol(ITransport transport, FEnetOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FEnetOptions();
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

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = FEnetAddress.Parse(request.Address, isBitAccess);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordElementsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = FEnetAddress.Parse(request.Address, isBitAccess);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordElementsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordElementsAsync(
        FEnetAddress address,
        PlcReadRequest request,
        CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        List<RequestChunker.Chunk> chunks = PlanWordChunks(address, request.Count, elementSize, _options.MaxReadBytes);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetInvokeIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FEnetAddress chunkAddress = address.WithByteOffset((long)chunk.ElementOffset * elementSize);
                ushort readByteCount = (ushort)(chunk.ElementCount * elementSize);
                byte[] chunkData = await ReadChunkAsync(session, chunkAddress.VariableName, readByteCount).ConfigureAwait(false);
                Array.Copy(chunkData, 0, combinedBuffer, chunk.ElementOffset * elementSize, readByteCount);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task WriteWordElementsAsync(
        FEnetAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        List<RequestChunker.Chunk> chunks = PlanWordChunks(address, request.Count, elementSize, _options.MaxWriteBytes);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetInvokeIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    FEnetAddress chunkAddress = address.WithByteOffset((long)chunk.ElementOffset * elementSize);
                    byte[] slice = request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    await WriteChunkAsync(session, chunkAddress.VariableName, slice).ConfigureAwait(false);
                    completedElements += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private static List<RequestChunker.Chunk> PlanWordChunks(
        FEnetAddress address,
        int elementCount,
        int elementSize,
        int maxChunkBytes)
    {
        ValidateWordAddressRange(address, elementCount, elementSize);
        int maxElements = RequestChunker.CalculateElementLimit(maxChunkBytes, elementSize);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(elementCount, maxElements);
        ValidateWordChunkOffsets(address, elementCount, elementSize, chunks);
        return chunks;
    }

    private static void ValidateWordAddressRange(FEnetAddress address, int elementCount, int elementSize)
    {
        long totalBytes = (long)elementCount * elementSize;
        long endOffset = totalBytes % address.ElementByteSize == 0
            ? (long)address.Offset + (totalBytes / address.ElementByteSize) - 1
            : ((long)address.Offset * address.ElementByteSize) + totalBytes - 1;

        if (endOffset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "요청 범위가 FEnet 32비트 주소 오프셋 한도를 초과합니다.");
        }
    }

    private static void ValidateWordChunkOffsets(
        FEnetAddress address,
        int elementCount,
        int elementSize,
        List<RequestChunker.Chunk> chunks)
    {
        foreach (RequestChunker.Chunk chunk in chunks)
        {
            long additionalBytes = (long)chunk.ElementOffset * elementSize;
            if (additionalBytes % address.ElementByteSize != 0
                && ((long)address.Offset * address.ElementByteSize) + additionalBytes > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "요청 청크 주소가 FEnet 32비트 오프셋 한도를 초과합니다.");
            }
        }
    }

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, string variableName, ushort readByteCount)
    {
        ushort invokeId = NextInvokeId();
        byte[] requestFrame = FEnetRequestBuilder.BuildContinuousRead(invokeId, variableName, readByteCount, _options.CpuInfo);

        await session.SendAsync(requestFrame).ConfigureAwait(false);
        byte[] responseFrame = await ReceiveMatchingResponseAsync(session, invokeId).ConfigureAwait(false);

        byte[] chunkData = FEnetResponseParser.ParseContinuousRead(responseFrame, invokeId);
        if (chunkData.Length != readByteCount)
        {
            throw new PlcException(
                PlcErrorKind.InvalidResponse,
                $"FEnet 읽기 응답 데이터 길이({chunkData.Length})가 요청 바이트 수({readByteCount})와 일치하지 않습니다.");
        }

        return chunkData;
    }

    private async Task WriteChunkAsync(ITransportSession session, string variableName, byte[] data)
    {
        ushort invokeId = NextInvokeId();
        byte[] requestFrame = FEnetRequestBuilder.BuildContinuousWrite(invokeId, variableName, data, _options.CpuInfo);

        await session.SendAsync(requestFrame).ConfigureAwait(false);
        byte[] responseFrame = await ReceiveMatchingResponseAsync(session, invokeId).ConfigureAwait(false);
        FEnetResponseParser.ParseContinuousWrite(responseFrame, invokeId);
    }

    private async Task<byte[]> ReceiveMatchingResponseAsync(ITransportSession session, ushort invokeId)
    {
        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (BinaryBuffer.ReadUInt16LittleEndian(responseFrame, InvokeIdOffset) == invokeId)
            {
                return responseFrame;
            }
        }
    }

    private ushort NextInvokeId()
    {
        lock (_invokeLock)
        {
            _invokeId = (ushort)(_invokeId == MaxInvokeId ? InitialInvokeId : _invokeId + 1);
            return _invokeId;
        }
    }

    private void ResetInvokeIdIfNewConnection(long connectionId)
    {
        lock (_invokeLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _invokeId = 0;
            }
        }
    }
}
