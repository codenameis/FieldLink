using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class MewtocolProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const byte StringPaddingByte = 0x00;
    private const string CommandEchoWriteContact = "WC";
    private const string CommandEchoWriteWords = "WD";

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly MewtocolOptions _options;
    private readonly MewtocolFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="MewtocolProtocol"/>을 초기화합니다.</summary>
    public MewtocolProtocol(ITransport transport, MewtocolOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new MewtocolOptions();
        _frameDecoder = new MewtocolFrameDecoder();
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

        var address = MewtocolAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        if (address.IsBit)
        {
            byte[] bitResults = new byte[request.Count];
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                for (int i = 0; i < request.Count; i++)
                {
                    MewtocolAddress bitAddress = address.Offset(i);
                    byte[] requestFrame = MewtocolRequestBuilder.BuildReadSingleBit(_options.Station, bitAddress);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    bitResults[i] = MewtocolResponseParser.ParseReadSingleBit(responseFrame, _options.Station);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);

            return bitResults;
        }

        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                MewtocolAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));

                byte[] requestFrame = MewtocolRequestBuilder.BuildReadWords(_options.Station, chunkAddress, wordCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte[] chunkData = MewtocolResponseParser.ParseReadWords(responseFrame, _options.Station, wordCount);

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

        var address = MewtocolAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int completedElements = 0;

        try
        {
            if (address.IsBit)
            {
                byte[] bitData = request.GetData();
                await _transport.RunExclusiveAsync<bool>(async session =>
                {
                    for (int i = 0; i < request.Count; i++)
                    {
                        MewtocolAddress bitAddress = address.Offset(i);
                        bool bitValue = bitData[i] != 0;
                        byte[] requestFrame = MewtocolRequestBuilder.BuildWriteSingleBit(_options.Station, bitAddress, bitValue);
                        byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                        MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, CommandEchoWriteContact);
                        completedElements++;
                    }

                    return true;
                }, cancellationToken).ConfigureAwait(false);
                return;
            }

            int wordsPerElement = elementSize / BytesPerWord;
            int maxElements = RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);
            List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);

            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    MewtocolAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                    ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = MewtocolRequestBuilder.BuildWriteWords(_options.Station, chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    MewtocolResponseParser.VerifyWriteResponse(responseFrame, _options.Station, CommandEchoWriteWords);
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

    private static void ValidateAddressAndDataType(MewtocolAddress address, PlcDataType dataType, int elementSize)
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
