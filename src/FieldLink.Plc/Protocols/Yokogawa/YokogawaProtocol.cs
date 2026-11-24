using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 higher-level link service 바이너리 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class YokogawaProtocol : IPlcProtocol
{
    private const int MaxWordReadChunk = 64;
    private const int MaxBitReadChunk = 256;
    private const int BytesPerWord = 2;
    private const byte StringPaddingByte = 0x00;
    private const byte CommandReadBit = 0x01;
    private const byte CommandWriteBit = 0x02;
    private const byte CommandReadWord = 0x11;
    private const byte CommandWriteWord = 0x12;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly YokogawaOptions _options;
    private readonly YokogawaFrameDecoder _frameDecoder = new();

    /// <summary><see cref="YokogawaProtocol"/>의 새 인스턴스를 초기화합니다.</summary>
    public YokogawaProtocol(ITransport transport, YokogawaOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YokogawaOptions();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public ITransport Transport => _transport;

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

        var address = YokogawaAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        if (address.IsBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        return await ReadWordsAsync(address, request, elementSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBitsAsync(YokogawaAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = checked((ushort)chunk.ElementCount);

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] bitBytes = YokogawaResponseParser.ParseReadResponse(responseFrame, CommandReadBit);
                if (bitBytes.Length != count)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"비트 응답 길이({bitBytes.Length})가 요청 개수({count})와 일치하지 않습니다.");
                }

                Array.Copy(bitBytes, 0, result, chunk.ElementOffset, count);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadWordsAsync(
        YokogawaAddress address,
        PlcReadRequest request,
        int elementSize,
        CancellationToken cancellationToken)
    {
        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(MaxWordReadChunk, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                YokogawaAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));

                byte[] requestFrame = YokogawaRequestBuilder.BuildReadRequest(_options.CpuNumber, chunkAddress, wordCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = YokogawaResponseParser.ParseReadResponse(responseFrame, CommandReadWord);
                int expectedBytes = checked(chunk.ElementCount * elementSize);
                if (chunkData.Length != expectedBytes)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"워드 응답 바이트 길이({chunkData.Length})가 요청 길이({expectedBytes})와 일치하지 않습니다.");
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

        var address = YokogawaAddress.Parse(request.Address);
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

    private async Task WriteBitsAsync(YokogawaAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int totalBits = request.Count;
        byte[] raw = request.GetData();
        byte[] bitBytes = new byte[totalBits];
        for (int i = 0; i < totalBits; i++)
        {
            bitBytes[i] = raw[i] != 0 ? BitOnValue : BitOffValue;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxBitReadChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    YokogawaAddress chunkAddress = address.Offset(chunk.ElementOffset);
                    ushort count = checked((ushort)chunk.ElementCount);
                    byte[] subBits = new byte[count];
                    Array.Copy(bitBytes, chunk.ElementOffset, subBits, 0, count);

                    byte[] requestFrame = YokogawaRequestBuilder.BuildWriteBitRequest(_options.CpuNumber, chunkAddress, count, subBits);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    YokogawaResponseParser.VerifyWriteResponse(responseFrame, CommandWriteBit);
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
        YokogawaAddress address,
        PlcWriteRequest request,
        int elementSize,
        CancellationToken cancellationToken)
    {
        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(MaxWordReadChunk, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    YokogawaAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                    ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = YokogawaRequestBuilder.BuildWriteWordRequest(_options.CpuNumber, chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    YokogawaResponseParser.VerifyWriteResponse(responseFrame, CommandWriteWord);
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

    private static void ValidateAddressAndDataType(YokogawaAddress address, PlcDataType dataType, int elementSize)
    {
        if (address.IsBit && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException($"비트 접점 '{address.Original}'에는 Boolean 자료형만 사용할 수 있습니다.", nameof(dataType));
        }

        if (!address.IsBit && elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address.Original}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(dataType));
        }
    }
}
