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
    private const int BytesPerWord = 2;
    private const int BytesPer32BitDevice = 4;

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
        await _transport.RunExclusiveAsync(async session =>
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
        await _transport.RunExclusiveAsync(async session =>
        {
            byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
            KeyenceHostLinkResponseParser.VerifyDisconnectResponse(responseFrame);
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
        bool isCompositeBitAccess = address.IsCompositeDevice && request.DataType == PlcDataType.Boolean;
        if (!isCompositeBitAccess)
        {
            ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
            Validate32BitDeviceDataType(address, request.DataType, request.ByteCount, nameof(request));
        }

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = (!address.IsBit && !isCompositeBitAccess && address.Is32BitDevice)
            ? BytesPer32BitDevice
            : BytesPerWord;
        PlcReadRequest effectiveRequest = (address.IsBit || isCompositeBitAccess)
            ? request
            : ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);

        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = (address.IsBit || isCompositeBitAccess)
            ? 1
            : Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        int maxElementsPerChunk = (address.IsBit || isCompositeBitAccess)
            ? _options.MaxReadElements
            : RequestChunker.CalculateElementLimit(_options.MaxReadElements, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                KeyenceHostLinkAddress chunkAddress = address.Offset(chunk.ElementOffset * unitsPerElement);
                ushort pointCount = (ushort)((address.IsBit || isCompositeBitAccess)
                    ? chunk.ElementCount
                    : chunk.ElementCount * unitsPerElement);

                byte[] chunkData = await ReadChunkAsync(
                    session,
                    chunkAddress,
                    pointCount,
                    isCompositeBitAccess,
                    bytesPerDeviceUnit).ConfigureAwait(false);
                int copyLength = Math.Min(chunkData.Length, combinedBuffer.Length - (chunk.ElementOffset * bytesPerElement));
                Array.Copy(chunkData, 0, combinedBuffer, chunk.ElementOffset * bytesPerElement, copyLength);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ProtocolExecutionGuard.TrimReadResult(combinedBuffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = KeyenceHostLinkAddress.Parse(request.Address);
        if (address.IsCompositeDevice)
        {
            throw new ArgumentException(
                $"복합 타이머/카운터 디바이스({address.Device})는 직접 쓸 수 없습니다. 현재값·설정값은 TC/TS/CC/CS를 사용하세요.",
                nameof(request));
        }

        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitDeviceDataType(address, request.DataType, request.ByteCount, nameof(request));

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        int bytesPerDeviceUnit = (!address.IsBit && address.Is32BitDevice)
            ? BytesPer32BitDevice
            : BytesPerWord;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit, bytesPerDeviceUnit);

        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = address.IsBit ? 1 : Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        int maxElementsPerChunk = address.IsBit
            ? _options.MaxWriteElements
            : RequestChunker.CalculateElementLimit(_options.MaxWriteElements, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    KeyenceHostLinkAddress chunkAddress = address.Offset(chunk.ElementOffset * unitsPerElement);
                    ushort pointCount = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * unitsPerElement);
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress, pointCount, chunkData, bytesPerDeviceUnit).ConfigureAwait(false);
                    completedElements = ProtocolExecutionGuard.AdvanceCompletedCount(
                        completedElements,
                        chunk.ElementCount,
                        originalElementsPerUnit,
                        totalOriginalCount);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private static void Validate32BitDeviceDataType(
        KeyenceHostLinkAddress address,
        PlcDataType dataType,
        int byteCount,
        string paramName)
    {
        if (!address.Is32BitDevice || address.IsBit)
        {
            return;
        }

        if (dataType is PlcDataType.Int16 or PlcDataType.UInt16 ||
            (dataType is (PlcDataType.Byte or PlcDataType.String) && byteCount % BytesPer32BitDevice != 0))
        {
            throw new ArgumentException(
                $"32비트 디바이스({address.Device})는 32비트 이상 자료형 또는 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        KeyenceHostLinkAddress chunkAddress,
        ushort pointCount,
        bool isCompositeBitAccess,
        int bytesPerDeviceUnit)
    {
        byte[] requestFrame = KeyenceHostLinkRequestBuilder.BuildRead(chunkAddress, pointCount);
        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

        if (chunkAddress.IsBit || isCompositeBitAccess)
        {
            return KeyenceHostLinkResponseParser.ParseReadBits(responseFrame, pointCount);
        }

        return bytesPerDeviceUnit == BytesPer32BitDevice
            ? KeyenceHostLinkResponseParser.ParseReadDWords(responseFrame, pointCount)
            : KeyenceHostLinkResponseParser.ParseReadWords(responseFrame, pointCount);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        KeyenceHostLinkAddress chunkAddress,
        ushort pointCount,
        byte[] chunkData,
        int bytesPerDeviceUnit)
    {
        byte[] requestFrame = chunkAddress.IsBit
            ? KeyenceHostLinkRequestBuilder.BuildWriteBits(chunkAddress, pointCount, chunkData)
            : (bytesPerDeviceUnit == BytesPer32BitDevice
                ? KeyenceHostLinkRequestBuilder.BuildWriteDWords(chunkAddress, pointCount, chunkData)
                : KeyenceHostLinkRequestBuilder.BuildWriteWords(chunkAddress, pointCount, chunkData));

        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
        KeyenceHostLinkResponseParser.VerifyWriteResponse(responseFrame);
    }
}
