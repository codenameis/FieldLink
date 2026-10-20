using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>TCP·UDP 위에서 실행되는 MC 프로토콜 바이너리 클라이언트입니다. 3E·4E 프레임, QL·iQ-R 시리즈를 지원합니다. (사양서 §1, §3)</summary>
public sealed class MCBinaryProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly MCOptions _options;
    private readonly MCBinaryFrameDecoder _frameDecoder;
    private readonly object _serialNumberLock = new();

    private ushort _serialNumber;
    private long _lastConnectionId = -1;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public MCBinaryProtocol(ITransport transport, MCOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new MCOptions();
        _frameDecoder = new MCBinaryFrameDecoder(_options.FrameType);
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

        var address = MCAddress.Parse(request.Address);
        int wordsPerElement = GetWordsPerElement(address, request.DataType);
        int elementSize = request.DataType.GetElementSize();
        List<RequestChunker.Chunk> chunks = PlanChunks(address, wordsPerElement, request.Count);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetSerialNumberIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                MCAddress chunkAddress = address.WithOffset(chunk.ElementOffset * wordsPerElement);
                ushort pointCount = (ushort)(chunk.ElementCount * wordsPerElement);

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress, pointCount).ConfigureAwait(false);
                Array.Copy(chunkData, 0, combinedBuffer, chunk.ElementOffset * elementSize, chunkData.Length);
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

        var address = MCAddress.Parse(request.Address);
        int wordsPerElement = GetWordsPerElement(address, request.DataType);
        List<RequestChunker.Chunk> chunks = PlanChunks(address, wordsPerElement, request.Count);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetSerialNumberIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    MCAddress chunkAddress = address.WithOffset(chunk.ElementOffset * wordsPerElement);
                    ushort pointCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    byte[] slice = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress, pointCount, slice).ConfigureAwait(false);
                    completedElements += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlcDeviceException exception)
        {
            throw new PlcDeviceException(exception.DeviceErrorCode, exception.Message) { CompletedCount = completedElements };
        }
        catch (PlcException exception)
        {
            throw new PlcException(exception.Kind, exception.Message, exception.InnerException) { CompletedCount = completedElements };
        }
    }

    private static int GetWordsPerElement(MCAddress address, PlcDataType dataType)
    {
        if (address.IsBit)
        {
            if (dataType != PlcDataType.Boolean)
            {
                throw new ArgumentException($"비트 디바이스 '{address}'는 Boolean 자료형만 읽고 쓸 수 있습니다.", nameof(dataType));
            }

            return 1;
        }

        int elementSize = dataType.GetElementSize();
        if (elementSize < 2 || elementSize % 2 != 0)
        {
            throw new ArgumentException($"워드 디바이스 '{address}'는 2바이트 배수 자료형만 읽고 쓸 수 있습니다. ({dataType})", nameof(dataType));
        }

        return elementSize / 2;
    }

    private List<RequestChunker.Chunk> PlanChunks(MCAddress address, int wordsPerElement, int elementCount)
    {
        int maxElements = address.IsBit
            ? _options.MaxBitPoints
            : RequestChunker.CalculateElementLimit(_options.MaxWordPoints, wordsPerElement);

        return RequestChunker.Plan(elementCount, maxElements);
    }

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, MCAddress address, ushort pointCount)
    {
        ushort serialNumber = NextSerialNumber();
        byte[] requestFrame = MCBinaryRequestBuilder.BuildReadRequest(_options, serialNumber, address, pointCount);

        await session.SendAsync(requestFrame).ConfigureAwait(false);
        byte[] responseFrame = await ReceiveMatchingResponseAsync(session, serialNumber).ConfigureAwait(false);

        return MCBinaryResponseParser.ParseRead(_options, serialNumber, responseFrame, address, pointCount);
    }

    private async Task WriteChunkAsync(ITransportSession session, MCAddress address, ushort pointCount, byte[] data)
    {
        ushort serialNumber = NextSerialNumber();
        byte[] requestFrame = MCBinaryRequestBuilder.BuildWriteRequest(_options, serialNumber, address, pointCount, data);

        await session.SendAsync(requestFrame).ConfigureAwait(false);
        byte[] responseFrame = await ReceiveMatchingResponseAsync(session, serialNumber).ConfigureAwait(false);

        MCBinaryResponseParser.ParseWrite(_options, serialNumber, responseFrame);
    }

    private async Task<byte[]> ReceiveMatchingResponseAsync(ITransportSession session, ushort serialNumber)
    {
        // 4E: 이전 요청의 늦은 응답은 시리얼 번호가 달라 버리고 계속 기다린다. 3E는 번호가 없어 첫 응답을 쓴다.
        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            bool isMatching = _options.FrameType == MCFrameType.Frame3E
                || MCBinaryResponseParser.ReadSerialNumber(responseFrame) == serialNumber;

            if (isMatching)
            {
                return responseFrame;
            }
        }
    }

    private ushort NextSerialNumber()
    {
        lock (_serialNumberLock)
        {
            _serialNumber = (ushort)(_serialNumber == ushort.MaxValue ? 1 : _serialNumber + 1);
            return _serialNumber;
        }
    }

    private void ResetSerialNumberIfNewConnection(long connectionId)
    {
        lock (_serialNumberLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _serialNumber = 0;
            }
        }
    }
}
