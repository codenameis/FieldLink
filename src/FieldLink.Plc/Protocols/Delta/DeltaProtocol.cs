using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta DVP 및 AS 시리즈 PLC와 통신하는 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class DeltaProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int DvpRelayBoundaryIndex = 1536;
    private const ushort DvpHighRelayBasePduAddress = 0xB000;
    private const int DvpRegisterBoundaryIndex = 4096;
    private const ushort DvpHighRegisterBasePduAddress = 0x9000;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly DeltaOptions _options;
    private readonly DeltaTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="DeltaProtocol"/> 인스턴스를 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">Delta 프로토콜 옵션입니다.</param>
    /// <param name="encoding">Modbus 전송 프레임 인코딩 방식입니다.</param>
    public DeltaProtocol(
        ITransport transport,
        DeltaOptions? options = null,
        DeltaTransportEncoding encoding = DeltaTransportEncoding.Rtu)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new DeltaOptions();
        _encoding = encoding;

        _frameDecoder = encoding switch
        {
            DeltaTransportEncoding.Rtu => new ModbusRtuFrameDecoder(),
            DeltaTransportEncoding.Ascii => new ModbusAsciiFrameDecoder(),
            DeltaTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "지원하지 않는 인코딩입니다."),
        };

        Traits = new PlcProtocolTraits(ByteOrder.BigEndian, DefaultStringFormat);
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

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = DeltaAddress.Parse(request.Address, _options.Series, isBitAccess);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = address.IsBit ? request : ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / BytesPerWord);
        int maxElements = address.IsBit
            ? _options.MaxReadBits
            : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

        List<PlannedChunk> chunks = PlanChunks(address, effectiveRequest.Count, maxElements, wordsPerElement);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (PlannedChunk chunk in chunks)
            {
                ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                byte[] chunkData = await ReadChunkAsync(session, address.Area, chunk.PduAddress, quantity).ConfigureAwait(false);
                int destinationOffset = chunk.ElementOffset * elementSize;
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ProtocolExecutionGuard.TrimReadResult(combinedBuffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBitAccess = request.DataType == PlcDataType.Boolean;
        var address = DeltaAddress.Parse(request.Address, _options.Series, isBitAccess);
        if (!address.IsWritable)
        {
            throw new ArgumentException($"쓰기 불가능한 영역입니다: {request.Address}", nameof(request));
        }

        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit);

        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, effectiveRequest.DataType.GetElementSize() / BytesPerWord);
        int maxElements = address.IsBit
            ? _options.MaxWriteBits
            : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

        List<PlannedChunk> chunks = PlanChunks(address, effectiveRequest.Count, maxElements, wordsPerElement);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (PlannedChunk chunk in chunks)
                {
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);
                    await WriteChunkAsync(session, chunk.PduAddress, address.IsBit, chunkData).ConfigureAwait(false);
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

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        ModbusArea area,
        ushort pduAddress,
        ushort quantity)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestPdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] requestAdu = WrapAdu(requestPdu, transactionId);

        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        bool isBit = area is ModbusArea.Coil or ModbusArea.DiscreteInput;
        return isBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, functionCode, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, functionCode, quantity);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        ushort pduAddress,
        bool isBit,
        byte[] chunkData)
    {
        ushort transactionId = NextTransactionId();
        ushort quantity = (ushort)(isBit ? chunkData.Length : chunkData.Length / BytesPerWord);
        byte[] requestPdu = BuildWritePdu(pduAddress, quantity, isBit, chunkData);

        byte[] requestAdu = WrapAdu(requestPdu, transactionId);
        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, pduAddress, quantity);
    }

    private static byte[] BuildWritePdu(ushort pduAddress, ushort quantity, bool isBit, byte[] chunkData)
    {
        if (!isBit)
        {
            return ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, chunkData);
        }

        bool[] bits = new bool[quantity];
        for (int i = 0; i < quantity; i++)
        {
            bits[i] = chunkData[i] != 0;
        }

        byte[] packedCoilBytes = BitPacking.Pack(bits);
        return ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, packedCoilBytes);
    }

    private byte[] WrapAdu(byte[] pdu, ushort transactionId) => _encoding switch
    {
        DeltaTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
        DeltaTransportEncoding.Ascii => ModbusRequestBuilder.BuildAsciiAdu(_options.Station, pdu),
        DeltaTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
        _ => throw new InvalidOperationException(),
    };

    private byte[] UnwrapAdu(byte[] responseAdu, ushort transactionId) => _encoding switch
    {
        DeltaTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(responseAdu, _options.Station),
        DeltaTransportEncoding.Ascii => ModbusResponseParser.UnwrapAsciiAdu(responseAdu, _options.Station),
        DeltaTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.Station),
        _ => throw new InvalidOperationException(),
    };

    private static List<PlannedChunk> PlanChunks(
        DeltaAddress address,
        int totalElements,
        int maxElements,
        int wordsPerElement)
    {
        List<RangePlan> ranges = PlanRanges(address, totalElements, wordsPerElement);
        var plannedChunks = new List<PlannedChunk>();
        int globalElementOffset = 0;

        foreach (RangePlan range in ranges)
        {
            List<RequestChunker.Chunk> subChunks = RequestChunker.Plan(range.ElementCount, maxElements);
            foreach (RequestChunker.Chunk subChunk in subChunks)
            {
                ushort pduAddress = (ushort)(range.BasePduAddress + (subChunk.ElementOffset * wordsPerElement));
                plannedChunks.Add(new PlannedChunk(pduAddress, globalElementOffset + subChunk.ElementOffset, subChunk.ElementCount));
            }

            globalElementOffset += range.ElementCount;
        }

        return plannedChunks;
    }

    private static List<RangePlan> PlanRanges(DeltaAddress address, int elementCount, int wordsPerElement)
    {
        var ranges = new List<RangePlan>(2);

        if (address.Series == DeltaSeries.Dvp)
        {
            if (address.Device == "M" && address.Index < DvpRelayBoundaryIndex && address.Index + elementCount > DvpRelayBoundaryIndex)
            {
                int firstElementCount = DvpRelayBoundaryIndex - address.Index;
                ranges.Add(new RangePlan(address.PduAddress, firstElementCount));
                ranges.Add(new RangePlan(DvpHighRelayBasePduAddress, elementCount - firstElementCount));
                return ranges;
            }

            int totalWords = elementCount * wordsPerElement;
            if (address.Device == "D" && address.Index < DvpRegisterBoundaryIndex && address.Index + totalWords > DvpRegisterBoundaryIndex)
            {
                if ((DvpRegisterBoundaryIndex - address.Index) % wordsPerElement != 0)
                {
                    throw new ArgumentException("다중 워드 원소가 불연속 주소 경계(D4096)에 걸쳐 있습니다.", nameof(address));
                }

                int firstElementCount = (DvpRegisterBoundaryIndex - address.Index) / wordsPerElement;
                if (firstElementCount > 0 && firstElementCount < elementCount)
                {
                    ranges.Add(new RangePlan(address.PduAddress, firstElementCount));
                    ranges.Add(new RangePlan(DvpHighRegisterBasePduAddress, elementCount - firstElementCount));
                    return ranges;
                }
            }
        }

        ranges.Add(new RangePlan(address.PduAddress, elementCount));
        return ranges;
    }

    private ushort NextTransactionId()
    {
        lock (_txIdLock)
        {
            _transactionId = (ushort)(_transactionId == ushort.MaxValue ? 1 : _transactionId + 1);
            return _transactionId;
        }
    }

    private void ResetTransactionIdIfNewConnection(long connectionId)
    {
        lock (_txIdLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _transactionId = 0;
            }
        }
    }

    private readonly record struct RangePlan(ushort BasePduAddress, int ElementCount);

    private readonly record struct PlannedChunk(ushort PduAddress, int ElementOffset, int ElementCount);
}
