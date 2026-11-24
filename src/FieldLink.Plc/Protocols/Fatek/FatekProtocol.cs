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
    private const int BytesPerWord = 2;
    private const int BytesPerDWord = 4;
    private const int WordsPerDWord = 2;
    private const byte StringPaddingByte = 0x00;
    private const string CommandWriteBits = "45";
    private const string CommandWriteWords = "47";

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

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
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitDeviceDataType(address, request.DataType, request.ByteCount, isWrite: false, nameof(request));

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = address.Is32Bit ? BytesPerDWord : BytesPerWord;
        PlcReadRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);
        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = address.IsBit ? 1 : Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        int wordsPerElement = address.IsBit
            ? 1
            : Math.Max(address.Is32Bit ? WordsPerDWord : 1, bytesPerElement / BytesPerWord);

        address.ValidateContiguousRange((long)effectiveRequest.Count * unitsPerElement, nameof(request));

        int maxElementsPerChunk = address.IsBit
            ? _options.MaxReadBits
            : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FatekAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * unitsPerElement));
                ushort pointCount = checked((ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * unitsPerElement));

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress, pointCount).ConfigureAwait(false);
                int bufferByteOffset = checked(chunk.ElementOffset * bytesPerElement);
                Array.Copy(chunkData, 0, combinedBuffer, bufferByteOffset, chunkData.Length);
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

        var address = FatekAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitDeviceDataType(address, request.DataType, request.ByteCount, isWrite: true, nameof(request));

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        int bytesPerDeviceUnit = address.Is32Bit ? BytesPerDWord : BytesPerWord;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit, bytesPerDeviceUnit);

        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int unitsPerElement = address.IsBit ? 1 : Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        int wordsPerElement = address.IsBit
            ? 1
            : Math.Max(address.Is32Bit ? WordsPerDWord : 1, bytesPerElement / BytesPerWord);

        address.ValidateContiguousRange((long)effectiveRequest.Count * unitsPerElement, nameof(request));

        int maxElementsPerChunk = address.IsBit
            ? _options.MaxWriteBits
            : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    FatekAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * unitsPerElement));
                    ushort pointCount = checked((ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * unitsPerElement));
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress, pointCount, chunkData).ConfigureAwait(false);
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
        FatekAddress address,
        PlcDataType dataType,
        int byteCount,
        bool isWrite,
        string paramName)
    {
        if (!address.Is32Bit)
        {
            return;
        }

        if (dataType is PlcDataType.Int16 or PlcDataType.UInt16 ||
            (isWrite && dataType is (PlcDataType.Byte or PlcDataType.String) && byteCount % BytesPerDWord != 0))
        {
            throw new ArgumentException(
                $"32비트 레지스터({address.Device})는 32비트 이상 자료형 또는 쓰기 시 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, FatekAddress chunkAddress, ushort pointCount)
    {
        byte[] requestFrame = chunkAddress.IsBit
            ? FatekRequestBuilder.BuildReadBits(_options.Station, chunkAddress, pointCount)
            : FatekRequestBuilder.BuildReadWords(_options.Station, chunkAddress, pointCount);

        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

        if (chunkAddress.IsBit)
        {
            return FatekResponseParser.ParseReadBits(responseFrame, _options.Station, pointCount);
        }

        return chunkAddress.Is32Bit
            ? FatekResponseParser.ParseReadDWords(responseFrame, _options.Station, pointCount)
            : FatekResponseParser.ParseReadWords(responseFrame, _options.Station, pointCount);
    }

    private async Task WriteChunkAsync(ITransportSession session, FatekAddress chunkAddress, ushort pointCount, byte[] chunkData)
    {
        byte[] requestFrame = chunkAddress.IsBit
            ? FatekRequestBuilder.BuildWriteBits(_options.Station, chunkAddress, pointCount, chunkData)
            : (chunkAddress.Is32Bit
                ? FatekRequestBuilder.BuildWriteDWords(_options.Station, chunkAddress, pointCount, chunkData)
                : FatekRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, pointCount, chunkData));

        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

        string expectedCommand = chunkAddress.IsBit ? CommandWriteBits : CommandWriteWords;
        FatekResponseParser.VerifyWriteResponse(responseFrame, _options.Station, expectedCommand);
    }
}
