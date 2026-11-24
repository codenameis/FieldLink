using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class GESrtpProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int BitsPerByte = 8;
    private const int SequenceMask = 0x7FFF;
    private const ushort InitialSequenceId = 1;
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly GESrtpOptions _options;
    private readonly GESrtpFrameDecoder _frameDecoder;
    private readonly object _seqLock = new();
    private int _sequenceCounter;
    private long _lastConnectionId = -1;

    /// <summary>전송 및 옵션으로 <see cref="GESrtpProtocol"/>을 초기화합니다.</summary>
    public GESrtpProtocol(ITransport transport, GESrtpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new GESrtpOptions();
        _frameDecoder = new GESrtpFrameDecoder();
        Traits = new PlcProtocolTraits(ByteOrder.LittleEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        byte[] initReq = GESrtpRequestBuilder.BuildInitSession();
        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetSequenceIfNewConnection(session.ConnectionId);
            byte[] initRes = await session.ExchangeAsync(initReq, _frameDecoder).ConfigureAwait(false);
            GESrtpResponseParser.ValidateInitResponse(initRes);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = GESrtpAddress.Parse(request.Address, isBitAccess);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int unitsPerElement = GetAddressUnitsPerElement(address, elementSize);
        int maxElements = GetMaxElements(address, _options.MaxReadBytes, elementSize, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetSequenceIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                GESrtpAddress chunkAddress = address.Offset(chunk.ElementOffset * unitsPerElement);
                ushort requestCount = (ushort)(chunk.ElementCount * unitsPerElement);
                ushort seq = NextSequenceId();

                byte[] requestFrame = GESrtpRequestBuilder.BuildReadRequest(seq, chunkAddress, requestCount);
                byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);

                int expectedBytes = address.IsBit
                    ? BitPacking.GetPackedByteCount(requestCount)
                    : chunk.ElementCount * elementSize;
                byte[] rawData = GESrtpResponseParser.ParseReadResponse(responseFrame, seq, expectedBytes);

                if (address.IsBit)
                {
                    bool[] bits = BitPacking.Unpack(rawData, 0, requestCount);
                    for (int i = 0; i < requestCount; i++)
                    {
                        combinedBuffer[chunk.ElementOffset + i] = (byte)(bits[i] ? 1 : 0);
                    }
                }
                else
                {
                    int destOffset = chunk.ElementOffset * elementSize;
                    Array.Copy(rawData, 0, combinedBuffer, destOffset, rawData.Length);
                }
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

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = GESrtpAddress.Parse(request.Address, isBitAccess);
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int unitsPerElement = GetAddressUnitsPerElement(address, elementSize);
        int maxElements = GetMaxElements(address, _options.MaxWriteBytes, elementSize, unitsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetSequenceIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    GESrtpAddress chunkAddress = address.Offset(chunk.ElementOffset * unitsPerElement);
                    ushort requestCount = (ushort)(chunk.ElementCount * unitsPerElement);
                    byte[] rawChunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    byte[] payload = address.IsBit ? PackBits(rawChunkData) : rawChunkData;
                    ushort seq = NextSequenceId();

                    byte[] requestFrame = GESrtpRequestBuilder.BuildWriteRequest(seq, chunkAddress, requestCount, payload);
                    byte[] responseFrame = await session.ExchangeAsync(requestFrame, _frameDecoder).ConfigureAwait(false);
                    GESrtpResponseParser.VerifyWriteResponse(responseFrame, seq);
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

    private static int GetAddressUnitsPerElement(GESrtpAddress address, int elementSize)
    {
        if (address.IsBit)
        {
            return 1;
        }

        return address.IsWord ? elementSize / BytesPerWord : elementSize;
    }

    private static int GetMaxElements(GESrtpAddress address, int maxBytes, int elementSize, int unitsPerElement)
    {
        if (address.IsBit)
        {
            return maxBytes * BitsPerByte;
        }

        if (address.IsWord)
        {
            return RequestChunker.CalculateElementLimit(maxBytes / BytesPerWord, unitsPerElement);
        }

        return RequestChunker.CalculateElementLimit(maxBytes, elementSize);
    }

    private static void ValidateAddressAndDataType(GESrtpAddress address, PlcDataType dataType, int elementSize)
    {
        if (dataType == PlcDataType.Boolean && !address.IsBit)
        {
            throw new ArgumentException($"워드 전용 영역 '{address.Original}'에는 Boolean 자료형을 사용할 수 없습니다.", nameof(dataType));
        }

        if (address.IsWord && elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address.Original}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(dataType));
        }
    }

    private static byte[] PackBits(byte[] rawBits)
    {
        bool[] bits = new bool[rawBits.Length];
        for (int i = 0; i < rawBits.Length; i++)
        {
            bits[i] = rawBits[i] != 0;
        }

        return BitPacking.Pack(bits);
    }

    private ushort NextSequenceId()
    {
        lock (_seqLock)
        {
            int next = _sequenceCounter + 1;
            if (next > SequenceMask)
            {
                next = InitialSequenceId;
            }

            _sequenceCounter = next;
            return (ushort)next;
        }
    }

    private void ResetSequenceIfNewConnection(long connectionId)
    {
        lock (_seqLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _sequenceCounter = 0;
            }
        }
    }
}
