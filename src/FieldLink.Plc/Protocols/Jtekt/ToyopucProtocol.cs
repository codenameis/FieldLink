using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 2PORT-EFR 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class ToyopucProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const byte BasicReadWordsCommand = 0x1C;
    private const byte ExtendedReadWordsCommand = 0x94;
    private const byte BasicWriteWordsCommand = 0x1D;
    private const byte ExtendedWriteWordsCommand = 0x95;
    private const byte WriteBitCommand = 0x21;
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly ToyopucOptions _options;
    private readonly ToyopucFrameDecoder _frameDecoder;

    /// <summary>전송 및 옵션으로 <see cref="ToyopucProtocol"/>을 초기화합니다.</summary>
    public ToyopucProtocol(ITransport transport, ToyopucOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new ToyopucOptions();
        _frameDecoder = new ToyopucFrameDecoder();
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = ToyopucAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            return await ReadBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int elementSize = request.DataType.GetElementSize();
        if (elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address.Original}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(request));
        }

        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ToyopucAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);

                byte[] requestFrame = ToyopucRequestBuilder.BuildReadWords(chunkAddress, wordCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                byte expectedCmd = chunkAddress.Prg.HasValue ? ExtendedReadWordsCommand : BasicReadWordsCommand;
                byte[] chunkData = ToyopucResponseParser.ParseReadWords(responseFrame, expectedCmd, wordCount);

                int destOffset = chunk.ElementOffset * elementSize;
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = ToyopucAddress.Parse(request.Address, isBit);

        if (isBit)
        {
            await WriteBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
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
                    ToyopucAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestFrame = ToyopucRequestBuilder.BuildWriteWords(chunkAddress, wordCount, chunkData);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    byte expectedCmd = chunkAddress.Prg.HasValue ? ExtendedWriteWordsCommand : BasicWriteWordsCommand;
                    ToyopucResponseParser.VerifyWriteResponse(responseFrame, expectedCmd);
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

    private async Task<byte[]> ReadBitsAsync(ToyopucAddress address, int count, CancellationToken cancellationToken)
    {
        if (address.Prg.HasValue)
        {
            throw new NotSupportedException($"확장 영역 '{address.Original}'에 대한 단일 비트 직접 읽기는 지원하지 않습니다.");
        }

        byte[] results = new byte[count];
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            for (int i = 0; i < count; i++)
            {
                byte[] requestFrame = ToyopucRequestBuilder.BuildReadBit(address.Offset(i));
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                results[i] = ToyopucResponseParser.ParseReadBit(responseFrame);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return results;
    }

    private async Task WriteBitsAsync(ToyopucAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (address.Prg.HasValue)
        {
            throw new NotSupportedException($"확장 영역 '{address.Original}'에 대한 단일 비트 직접 쓰기는 지원하지 않습니다.");
        }

        byte[] values = request.GetData();
        int completedBits = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                for (int i = 0; i < request.Count; i++)
                {
                    bool value = values[i] != 0;
                    byte[] requestFrame = ToyopucRequestBuilder.BuildWriteBit(address.Offset(i), value);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    ToyopucResponseParser.VerifyWriteResponse(responseFrame, expectedCommand: WriteBitCommand);
                    completedBits++;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlcDeviceException deviceException)
        {
            throw new PlcDeviceException(deviceException.DeviceErrorCode, deviceException.Message) { CompletedCount = completedBits };
        }
        catch (PlcException exception)
        {
            throw new PlcException(exception.Kind, exception.Message, exception.InnerException)
            {
                CompletedCount = completedBits
            };
        }
    }
}
