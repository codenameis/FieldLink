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
    private const int MaxBitReadChunk = 1024;
    private const int MaxWordReadChunk = 64;
    private const int Max32BitWordReadChunk = 32;
    private const int BytesPerWord = 2;
    private const int BytesPer32BitWord = 4;
    private const int BitsPerByte = 8;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;
    private const byte StringPaddingByte = 0x00;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

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
    public ITransport Transport => _transport;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = VigorAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, elementSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(VigorAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = checked((ushort)chunk.ElementCount);

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] packed = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
                int requiredBytes = (chunk.ElementCount + BitsPerByte - 1) / BitsPerByte;
                if (packed.Length < requiredBytes)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"비트 응답 길이가 부족합니다 (필요: {requiredBytes}, 수신: {packed.Length}).");
                }

                for (int i = 0; i < count; i++)
                {
                    bool bit = (packed[i / BitsPerByte] & (1 << (i % BitsPerByte))) != 0;
                    result[chunk.ElementOffset + i] = bit ? BitOnValue : BitOffValue;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(
        VigorAddress address,
        PlcReadRequest request,
        int elementSize,
        CancellationToken cancellationToken)
    {
        int unitBytes = address.Is32BitWord ? BytesPer32BitWord : BytesPerWord;
        int unitsPerElement = elementSize / unitBytes;
        int maxChunkUnits = address.Is32BitWord ? Max32BitWordReadChunk : MaxWordReadChunk;
        int maxElements = RequestChunker.CalculateElementLimit(maxChunkUnits, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                VigorAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * unitsPerElement));
                ushort unitCount = checked((ushort)(chunk.ElementCount * unitsPerElement));

                byte[] requestFrame = VigorRequestBuilder.BuildReadRequest(_options.StationNumber, chunkAddress, unitCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
                int expectedBytes = checked(chunk.ElementCount * elementSize);
                if (chunkData.Length != expectedBytes)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"워드 응답 바이트 길이가 요청({expectedBytes})과 다릅니다: {chunkData.Length}");
                }

                int destOffset = checked(chunk.ElementOffset * elementSize);
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

        var address = VigorAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        if (address.IsBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteWordsAsync(address, request, elementSize, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteBitsAsync(VigorAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int count = request.Count;
        byte[] raw = request.GetData();
        bool[] bools = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bools[i] = raw[i] != 0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(count, MaxBitReadChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    VigorAddress chunkAddress = address.Offset(chunk.ElementOffset);
                    bool[] subBools = new bool[chunk.ElementCount];
                    Array.Copy(bools, chunk.ElementOffset, subBools, 0, chunk.ElementCount);

                    byte[] requestFrame = VigorRequestBuilder.BuildWriteBitRequest(_options.StationNumber, chunkAddress, subBools);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
                    completedElements += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlcDeviceException deviceException)
        {
            throw new PlcDeviceException(deviceException.DeviceErrorCode, deviceException.Message) { CompletedCount = completedElements };
        }
        catch (PlcException exception)
        {
            throw new PlcException(exception.Kind, exception.Message, exception.InnerException)
            {
                CompletedCount = completedElements
            };
        }
    }

    private async Task WriteWordsAsync(
        VigorAddress address,
        PlcWriteRequest request,
        int elementSize,
        CancellationToken cancellationToken)
    {
        int unitBytes = address.Is32BitWord ? BytesPer32BitWord : BytesPerWord;
        int unitsPerElement = elementSize / unitBytes;
        int maxChunkUnits = address.Is32BitWord ? Max32BitWordReadChunk : MaxWordReadChunk;
        int maxElements = RequestChunker.CalculateElementLimit(maxChunkUnits, unitsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    VigorAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * unitsPerElement));
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = VigorRequestBuilder.BuildWriteWordRequest(_options.StationNumber, chunkAddress, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    VigorResponseParser.ParseResponse(responseFrame, _options.StationNumber);
                    completedElements += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlcDeviceException deviceException)
        {
            throw new PlcDeviceException(deviceException.DeviceErrorCode, deviceException.Message) { CompletedCount = completedElements };
        }
        catch (PlcException exception)
        {
            throw new PlcException(exception.Kind, exception.Message, exception.InnerException)
            {
                CompletedCount = completedElements
            };
        }
    }

    private static void ValidateAddressAndDataType(VigorAddress address, PlcDataType dataType, int elementSize)
    {
        if (address.IsBit && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException($"비트 접점 '{address.Original}'에는 Boolean 자료형만 사용할 수 있습니다.", nameof(dataType));
        }

        if (!address.IsBit)
        {
            int requiredUnitBytes = address.Is32BitWord ? BytesPer32BitWord : BytesPerWord;
            if (elementSize % requiredUnitBytes != 0)
            {
                throw new ArgumentException(
                    $"워드 영역 '{address.Original}'에는 {requiredUnitBytes}바이트 배수 자료형만 사용할 수 있습니다.",
                    nameof(dataType));
            }
        }
    }
}
