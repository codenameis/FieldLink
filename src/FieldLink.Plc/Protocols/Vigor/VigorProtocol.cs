using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 Computer Link 프로토콜 클라이언트입니다. (사양서 §1~§3)</summary>
public sealed class VigorProtocol : IPlcProtocol
{
    private const int Max32BitCountersPerRequest = 32;
    private const int BytesPer16BitWord = 2;
    private const int BytesPer32BitCounter = 4;
    private const int BitsPerByte = 8;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly VigorOptions _options;
    private readonly VigorFrameDecoder _frameDecoder = new();

    /// <summary><see cref="VigorProtocol"/> 새 인스턴스를 초기화합니다.</summary>
    public VigorProtocol(ITransport transport, VigorOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new VigorOptions();
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

        var address = VigorAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitCounterDataType(address, request.DataType, request.ByteCount, nameof(request));

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPer16BitWord;
        PlcReadRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);
        byte[] buffer = await ReadWordsAsync(address, effectiveRequest, cancellationToken).ConfigureAwait(false);
        return ProtocolExecutionGuard.TrimReadResult(buffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = VigorAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitCounterDataType(address, request.DataType, request.ByteCount, nameof(request));

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate32BitCounterDataType(
        VigorAddress address,
        PlcDataType dataType,
        int byteCount,
        string paramName)
    {
        if (!address.Is32BitWord)
        {
            return;
        }

        if (dataType is PlcDataType.Int16 or PlcDataType.UInt16 ||
            (dataType is (PlcDataType.Byte or PlcDataType.String) && byteCount % BytesPer32BitCounter != 0))
        {
            throw new ArgumentException(
                "32비트 카운터(C200~C255)는 32비트 이상 자료형 또는 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private async Task<byte[]> ReadBitsAsync(VigorAddress address, int totalBits, CancellationToken cancellationToken)
    {
        ValidateDeviceRange(address, totalBits, nameof(totalBits));
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, _options.MaxBitsPerRequest);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort bitCount = (ushort)chunk.ElementCount;

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, bitCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] packedBits = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);

                for (int i = 0; i < bitCount; i++)
                {
                    bool bitValue = (packedBits[i / BitsPerByte] & (1 << (i % BitsPerByte))) != 0;
                    result[chunk.ElementOffset + i] = bitValue ? (byte)1 : (byte)0;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(VigorAddress address, PlcReadRequest effectiveRequest, CancellationToken cancellationToken)
    {
        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPer16BitWord;
        int deviceUnitsPerElement = Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        ValidateDeviceRange(address, effectiveRequest.Count * deviceUnitsPerElement, nameof(effectiveRequest));

        int maxDeviceUnits = GetMaxDeviceUnitsPerRequest(address);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(maxDeviceUnits, deviceUnitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset * deviceUnitsPerElement);
                ushort deviceUnitCount = (ushort)(chunk.ElementCount * deviceUnitsPerElement);
                int expectedByteCount = deviceUnitCount * bytesPerDeviceUnit;

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, deviceUnitCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);

                if (chunkData.Length != expectedByteCount)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"VIGOR 응답 데이터 바이트 수가 일치하지 않습니다: 기대={expectedByteCount}, 실제={chunkData.Length}");
                }

                int bufferByteOffset = chunk.ElementOffset * bytesPerElement;
                Array.Copy(chunkData, 0, combinedBuffer, bufferByteOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task WriteBitsAsync(VigorAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalBits = request.Count;
        ValidateDeviceRange(address, totalBits, nameof(request));
        byte[] rawBytes = request.GetData();
        bool[] bitValues = new bool[totalBits];
        for (int i = 0; i < totalBits; i++)
        {
            bitValues[i] = rawBytes[i] != 0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, _options.MaxBitsPerRequest);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                    bool[] chunkBits = new bool[chunk.ElementCount];
                    Array.Copy(bitValues, chunk.ElementOffset, chunkBits, 0, chunk.ElementCount);

                    byte[] requestFrame = VigorRequestBuilder.BuildWriteBitRequest(_options.StationNumber, chunkAddress, chunkBits);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
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

    private async Task WriteWordsAsync(VigorAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalOriginalCount = request.Count;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPer16BitWord;
        PlcWriteRequest effectiveRequest = ProtocolExecutionGuard.NormalizeWordWriteRequest(
            request,
            out int originalElementsPerUnit,
            bytesPerDeviceUnit);

        int bytesPerElement = effectiveRequest.DataType.GetElementSize();
        int deviceUnitsPerElement = Math.Max(1, bytesPerElement / bytesPerDeviceUnit);
        ValidateDeviceRange(address, effectiveRequest.Count * deviceUnitsPerElement, nameof(request));
        int maxDeviceUnits = GetMaxDeviceUnitsPerRequest(address);
        int maxElementsPerChunk = RequestChunker.CalculateElementLimit(maxDeviceUnits, deviceUnitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElementsPerChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    VigorAddress chunkAddress = address.Offset(chunk.ElementOffset * deviceUnitsPerElement);
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = VigorRequestBuilder.BuildWriteWordRequest(_options.StationNumber, chunkAddress, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
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

    private static void ValidateDeviceRange(VigorAddress address, int totalDeviceUnits, string paramName)
    {
        try
        {
            address.Offset(Math.Max(0, totalDeviceUnits - 1));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentException("요청 범위가 디바이스 영역 경계를 벗어났습니다.", paramName, exception);
        }
    }

    private int GetMaxDeviceUnitsPerRequest(VigorAddress address)
    {
        return address.Is32BitWord
            ? Math.Min(_options.MaxWordsPerRequest, Max32BitCountersPerRequest)
            : _options.MaxWordsPerRequest;
    }
}
