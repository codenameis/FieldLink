using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>TCP 소켓 상에서 실행되는 Modbus TCP 클라이언트 프로토콜 구현체입니다. (사양서 §1, §3)</summary>
public sealed class ModbusTcpProtocol : IPlcProtocol
{
    private const int BytesPerRegister = 2;
    private const int ByteShift = 8;
    private const byte StringPaddingByte = 0x00;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort WrappedTransactionId = 0x0000;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerRegister);

    private readonly ITransport _transport;
    private readonly ModbusTcpOptions _options;
    private readonly ModbusTcpFrameDecoder _frameDecoder = new();
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public ModbusTcpProtocol(ITransport transport, ModbusTcpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new ModbusTcpOptions();
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

        var address = ModbusAddress.Parse(request.Address);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = address.IsBit ? request : ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, effectiveRequest.DataType.GetElementSize() / BytesPerRegister);
        ValidateAddressRange(address, effectiveRequest.Count, wordsPerElement);

        List<RequestChunker.Chunk> chunks = PlanReadChunks(address.IsBit, effectiveRequest.Count, wordsPerElement);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                byte[] chunkData = await ReadChunkAsync(session, address, chunk, wordsPerElement).ConfigureAwait(false);
                int destinationOffset = checked(chunk.ElementOffset * effectiveRequest.DataType.GetElementSize());
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

        var address = ModbusAddress.Parse(request.Address);
        if (!address.IsWritable)
        {
            throw new ArgumentException("읽기 전용 영역(이산 입력, 입력 레지스터)에는 쓸 수 없습니다.", nameof(request));
        }

        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit);

        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, effectiveRequest.DataType.GetElementSize() / BytesPerRegister);
        ValidateAddressRange(address, effectiveRequest.Count, wordsPerElement);

        List<RequestChunker.Chunk> chunks = PlanWriteChunks(address.IsBit, effectiveRequest.Count, wordsPerElement);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    await WriteChunkAsync(session, address, chunk, effectiveRequest).ConfigureAwait(false);
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

    private static void ValidateAddressRange(ModbusAddress address, int elementCount, int wordsPerElement)
    {
        long totalUnits = address.IsBit ? elementCount : (long)elementCount * wordsPerElement;
        if ((long)address.PduAddress + totalUnits - 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elementCount),
                elementCount,
                $"요청 범위가 Modbus 16비트 주소 공간(0~{ushort.MaxValue})을 초과합니다.");
        }
    }

    private List<RequestChunker.Chunk> PlanReadChunks(bool isBit, int count, int wordsPerElement)
    {
        int maxElements = isBit
            ? _options.MaxReadCoils
            : RequestChunker.CalculateElementLimit(_options.MaxReadRegisters, wordsPerElement);
        return RequestChunker.Plan(count, maxElements);
    }

    private List<RequestChunker.Chunk> PlanWriteChunks(bool isBit, int count, int wordsPerElement)
    {
        int maxElements = isBit
            ? _options.MaxWriteCoils
            : RequestChunker.CalculateElementLimit(_options.MaxWriteRegisters, wordsPerElement);
        return RequestChunker.Plan(count, maxElements);
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        ModbusAddress address,
        RequestChunker.Chunk chunk,
        int wordsPerElement)
    {
        ushort chunkPduAddress = (ushort)(address.PduAddress + (chunk.ElementOffset * wordsPerElement));
        ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, chunkPduAddress, quantity);
        byte[] responsePdu = await ExchangeWithLateResponseDiscardAsync(session, pdu).ConfigureAwait(false);

        return address.IsBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, (ModbusFunctionCode)pdu[0], quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, (ModbusFunctionCode)pdu[0], quantity);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        ModbusAddress address,
        RequestChunker.Chunk chunk,
        PlcWriteRequest request)
    {
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, request.DataType.GetElementSize() / BytesPerRegister);
        ushort chunkPduAddress = (ushort)(address.PduAddress + (chunk.ElementOffset * wordsPerElement));
        byte[] pdu = BuildWritePdu(address, chunkPduAddress, chunk, request);

        byte[] responsePdu = await ExchangeWithLateResponseDiscardAsync(session, pdu).ConfigureAwait(false);
        ushort expectedQuantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
        ValidateWriteResponse(pdu, responsePdu, chunkPduAddress, expectedQuantity);
    }

    private byte[] BuildWritePdu(
        ModbusAddress address,
        ushort chunkPduAddress,
        RequestChunker.Chunk chunk,
        PlcWriteRequest request)
    {
        if (address.IsBit)
        {
            if (_options.UseSingleWriteFunctions && chunk.ElementCount == 1)
            {
                byte[] raw = request.GetData(chunk.ElementOffset, 1);
                return ModbusRequestBuilder.BuildWriteSingleCoil(chunkPduAddress, raw[0] != 0);
            }

            byte[] bitData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
            bool[] bits = new bool[chunk.ElementCount];
            for (int i = 0; i < chunk.ElementCount; i++)
            {
                bits[i] = bitData[i] != 0;
            }

            byte[] packed = BitPacking.Pack(bits);
            return ModbusRequestBuilder.BuildWriteMultipleCoils(chunkPduAddress, (ushort)chunk.ElementCount, packed);
        }

        int wordsPerElement = Math.Max(1, request.DataType.GetElementSize() / BytesPerRegister);
        if (_options.UseSingleWriteFunctions && chunk.ElementCount == 1 && wordsPerElement == 1)
        {
            byte[] raw = request.GetData(chunk.ElementOffset, 1);
            ushort registerValue = (ushort)((raw[0] << ByteShift) | raw[1]);
            return ModbusRequestBuilder.BuildWriteSingleRegister(chunkPduAddress, registerValue);
        }

        byte[] registerSlice = request.GetData(chunk.ElementOffset, chunk.ElementCount);
        ushort registerCount = (ushort)(chunk.ElementCount * wordsPerElement);
        return ModbusRequestBuilder.BuildWriteMultipleRegisters(chunkPduAddress, registerCount, registerSlice);
    }

    private static void ValidateWriteResponse(
        byte[] requestPdu,
        byte[] responsePdu,
        ushort chunkPduAddress,
        ushort expectedQuantity)
    {
        var functionCode = (ModbusFunctionCode)requestPdu[0];
        if (functionCode is ModbusFunctionCode.WriteSingleCoil or ModbusFunctionCode.WriteSingleRegister)
        {
            ModbusResponseParser.ParseWriteSingleResponse(responsePdu, requestPdu);
        }
        else
        {
            ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, chunkPduAddress, expectedQuantity);
        }
    }

    private async Task<byte[]> ExchangeWithLateResponseDiscardAsync(ITransportSession session, byte[] pdu)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestAdu = ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.UnitId, pdu);

        await session.SendAsync(requestAdu).ConfigureAwait(false);

        // 사양서 §6 & §8-4 E4: 이전 요청의 늦은 응답은 트랜잭션 ID가 다르므로 버리고 계속 수신
        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            ushort receivedTxId = (ushort)((responseAdu[0] << ByteShift) | responseAdu[1]);

            if (receivedTxId == transactionId)
            {
                return ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.UnitId);
            }
        }
    }

    private ushort NextTransactionId()
    {
        lock (_txIdLock)
        {
            _transactionId = (ushort)(_transactionId == MaxTransactionId ? WrappedTransactionId : _transactionId + 1);
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
                _transactionId = WrappedTransactionId;
            }
        }
    }
}
