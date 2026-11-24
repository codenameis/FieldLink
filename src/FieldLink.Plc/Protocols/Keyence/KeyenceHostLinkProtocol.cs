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
    private const int BitByteSize = 1;
    private const byte StringPaddingByte = 0x00;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

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
        await _transport.RunExclusiveAsync<bool>(async session =>
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
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            await session.SendAsync(requestFrame).ConfigureAwait(false);
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
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        int maxElements = address.IsBit
            ? _options.MaxReadElements
            : RequestChunker.CalculateElementLimit(_options.MaxReadElements, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                int chunkOffsetUnits = checked(chunk.ElementOffset * wordsPerElement);
                KeyenceHostLinkAddress chunkAddress = address.Offset(chunkOffsetUnits);
                int rawCount = checked(chunk.ElementCount * wordsPerElement);
                ushort count = checked((ushort)rawCount);

                byte[] requestFrame = KeyenceHostLinkRequestBuilder.BuildRead(chunkAddress, count);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                byte[] chunkData = address.IsBit
                    ? KeyenceHostLinkResponseParser.ParseReadBits(responseFrame, count)
                    : KeyenceHostLinkResponseParser.ParseReadWords(responseFrame, count);

                int destOffset = checked(chunk.ElementOffset * (address.IsBit ? BitByteSize : elementSize));
                int expectedChunkBytes = checked(chunk.ElementCount * (address.IsBit ? BitByteSize : elementSize));
                if (chunkData.Length != expectedChunkBytes)
                {
                    throw new PlcException(
                        PlcErrorKind.InvalidResponse,
                        $"응답 바이트 길이가 요청({expectedChunkBytes})과 다릅니다: {chunkData.Length}");
                }

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

        var address = KeyenceHostLinkAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        int maxElements = address.IsBit
            ? _options.MaxWriteElements
            : RequestChunker.CalculateElementLimit(_options.MaxWriteElements, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    int chunkOffsetUnits = checked(chunk.ElementOffset * wordsPerElement);
                    KeyenceHostLinkAddress chunkAddress = address.Offset(chunkOffsetUnits);
                    ushort count = checked((ushort)(chunk.ElementCount * wordsPerElement));
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = address.IsBit
                        ? KeyenceHostLinkRequestBuilder.BuildWriteBits(chunkAddress, count, chunkData)
                        : KeyenceHostLinkRequestBuilder.BuildWriteWords(chunkAddress, count, chunkData);

                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    KeyenceHostLinkResponseParser.VerifyWriteResponse(responseFrame);
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

    private static void ValidateAddressAndDataType(KeyenceHostLinkAddress address, PlcDataType dataType, int elementSize)
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
