using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>TCP 소켓 상에서 실행되는 LS ELECTRIC XGT FEnet 클라이언트 프로토콜 구현체입니다. (사양서 §1, §3)</summary>
public sealed class FEnetProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly FEnetOptions _options;
    private readonly FEnetFrameDecoder _frameDecoder = new();
    private readonly object _invokeLock = new();

    private ushort _invokeId;
    private long _lastConnectionId = -1;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public FEnetProtocol(ITransport transport, FEnetOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FEnetOptions();
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

        var address = FEnetAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxReadBytes, elementSize);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetInvokeIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                var chunkAddress = address.WithOffset(chunk.ElementOffset);
                ushort readByteCount = (ushort)(chunk.ElementCount * elementSize);

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress.VariableName, readByteCount).ConfigureAwait(false);

                int destinationOffset = chunk.ElementOffset * elementSize;
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
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

        var address = FEnetAddress.Parse(request.Address);
        int elementSize = request.DataType.GetElementSize();
        int maxElements = RequestChunker.CalculateElementLimit(_options.MaxWriteBytes, elementSize);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetInvokeIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    var chunkAddress = address.WithOffset(chunk.ElementOffset);
                    byte[] slice = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress.VariableName, slice).ConfigureAwait(false);
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

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, string variableName, ushort readByteCount)
    {
        ushort invokeId = NextInvokeId();
        byte[] requestFrame = FEnetRequestBuilder.BuildContinuousRead(invokeId, variableName, readByteCount, _options.CpuInfo);

        await session.SendAsync(requestFrame).ConfigureAwait(false);

        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            ushort receivedInvokeId = (ushort)(responseFrame[14] | (responseFrame[15] << 8));

            if (receivedInvokeId == invokeId)
            {
                return FEnetResponseParser.ParseContinuousRead(responseFrame, invokeId);
            }
        }
    }

    private async Task WriteChunkAsync(ITransportSession session, string variableName, byte[] data)
    {
        ushort invokeId = NextInvokeId();
        byte[] requestFrame = FEnetRequestBuilder.BuildContinuousWrite(invokeId, variableName, data, _options.CpuInfo);

        await session.SendAsync(requestFrame).ConfigureAwait(false);

        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            ushort receivedInvokeId = (ushort)(responseFrame[14] | (responseFrame[15] << 8));

            if (receivedInvokeId == invokeId)
            {
                FEnetResponseParser.ParseContinuousWrite(responseFrame, invokeId);
                return;
            }
        }
    }

    private ushort NextInvokeId()
    {
        lock (_invokeLock)
        {
            _invokeId = (ushort)(_invokeId == 0xFFFF ? 0x0001 : _invokeId + 1);
            return _invokeId;
        }
    }

    private void ResetInvokeIdIfNewConnection(long connectionId)
    {
        lock (_invokeLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _invokeId = 0;
            }
        }
    }
}
