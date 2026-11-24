using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 로더 프로토콜 드라이버입니다. (사양서 Part C)</summary>
public sealed class FujiElectricSphProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int BitsPerWord = 16;
    private const int BitsPerByte = 8;
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly FujiElectricSphOptions _options;
    private readonly FujiElectricSphFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="FujiElectricSphProtocol"/>을 초기화합니다.</summary>
    public FujiElectricSphProtocol(ITransport transport, FujiElectricSphOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FujiElectricSphOptions();
        _frameDecoder = new FujiElectricSphFrameDecoder();
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

        var address = FujiElectricSphAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"비트 주소 '{address.Original}'에는 Boolean 자료형만 사용할 수 있습니다.", nameof(request));
        }

        return await ReadWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordsAsync(FujiElectricSphAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        if (elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address.Original}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(request));
        }

        int wordsPerElement = elementSize / BytesPerWord;
        int totalWords = request.Count * wordsPerElement;
        return await ReadWordSpanAsync(address, totalWords, request.ByteCount, wordsPerElement, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadWordSpanAsync(
        FujiElectricSphAddress address,
        int totalWords,
        int totalBytes,
        int wordsPerElement,
        CancellationToken cancellationToken)
    {
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        int maxWordsPerChunk = maxElements * wordsPerElement;
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, maxWordsPerChunk);
        byte[] combinedBuffer = new byte[totalBytes];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                FujiElectricSphAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;

                byte[] requestFrame = FujiElectricSphRequestBuilder.BuildReadWords(_options.ConnectionId, chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] data = FujiElectricSphResponseParser.ParseReadWords(responseFrame, _options.ConnectionId, count);

                int destOffset = chunk.ElementOffset * BytesPerWord;
                Array.Copy(data, 0, combinedBuffer, destOffset, data.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadBitsAsync(FujiElectricSphAddress address, int bitCount, CancellationToken cancellationToken)
    {
        long startBit = ((long)address.WordAddress * BitsPerWord) + address.BitOffset;
        long endBit = startBit + bitCount - 1;
        long startWord = startBit / BitsPerWord;
        long endWord = endBit / BitsPerWord;
        int totalWords = (int)(endWord - startWord + 1);

        FujiElectricSphAddress baseWordAddr = address.Offset((int)(startWord - address.WordAddress));
        byte[] wordBytes = await ReadWordSpanAsync(baseWordAddr, totalWords, totalWords * BytesPerWord, wordsPerElement: 1, cancellationToken).ConfigureAwait(false);

        byte[] bitResult = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            long absoluteBit = startBit + i;
            int wordIdx = (int)((absoluteBit / BitsPerWord) - startWord);
            int bitInWord = (int)(absoluteBit % BitsPerWord);
            int byteOffset = (wordIdx * BytesPerWord) + (bitInWord / BitsPerByte);
            int bitInByte = bitInWord % BitsPerByte;

            bool isSet = (wordBytes[byteOffset] & (1 << bitInByte)) != 0;
            bitResult[i] = (byte)(isSet ? 1 : 0);
        }

        return bitResult;
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = FujiElectricSphAddress.Parse(request.Address);
        if (request.DataType == PlcDataType.Boolean || address.IsBit)
        {
            throw new NotSupportedException("MICREX-SX SPH 프로토콜은 비트 쓰기를 지원하지 않습니다.");
        }

        int elementSize = request.DataType.GetElementSize();
        if (elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address.Original}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(request));
        }

        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    FujiElectricSphAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = FujiElectricSphRequestBuilder.BuildWriteWords(_options.ConnectionId, chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    FujiElectricSphResponseParser.VerifyWriteResponse(responseFrame, _options.ConnectionId);
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
}
