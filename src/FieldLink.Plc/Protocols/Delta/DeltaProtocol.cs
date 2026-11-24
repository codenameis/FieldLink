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
    private const int BitsPerWord = 16;
    private const byte StringPaddingByte = 0x00;
    private const int DvpRelayBoundaryIndex = 1536;
    private const ushort DvpHighRelayBasePduAddress = 0xB000;
    private const int DvpRegisterBoundaryIndex = 4096;
    private const ushort DvpHighRegisterBasePduAddress = 0x9000;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly DeltaOptions _options;
    private readonly DeltaTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _transactionIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="DeltaProtocol"/> 인스턴스를 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">Delta 프로토콜 옵션입니다.</param>
    /// <param name="encoding">Modbus 전송 프레임 인코딩 방식입니다.</param>
    public DeltaProtocol(
        ITransport transport,
        DeltaOptions? options = null,
        DeltaTransportEncoding encoding = DeltaTransportEncoding.Tcp)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new DeltaOptions();
        _encoding = encoding;
        _frameDecoder = encoding switch
        {
            DeltaTransportEncoding.Rtu => new ModbusRtuFrameDecoder(),
            DeltaTransportEncoding.Ascii => new ModbusAsciiFrameDecoder(),
            DeltaTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "지원하지 않는 Delta 전송 인코딩입니다.")
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

        if (address.IsWordBitAccess)
        {
            return await ReadWordBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = address.IsBit ? request : ProtocolExecutionGuard.NormalizeWordReadRequest(request);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / BytesPerWord);
        address.ValidateContiguousRange(effectiveRequest.Count, wordsPerElement, nameof(request));

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
                ushort quantity = checked((ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement));
                byte[] chunkData = await ReadChunkAsync(session, address.Area, chunk.PduAddress, quantity).ConfigureAwait(false);
                int destinationOffset = checked(chunk.ElementOffset * elementSize);
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

        if (address.IsWordBitAccess)
        {
            await WriteWordBitAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit);

        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, effectiveRequest.DataType.GetElementSize() / BytesPerWord);
        address.ValidateContiguousRange(effectiveRequest.Count, wordsPerElement, nameof(request));

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
                    await WriteChunkAsync(session, chunk.PduAddress, address.IsBit, chunk.ElementCount, wordsPerElement, chunkData).ConfigureAwait(false);
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

    private async Task<byte[]> ReadWordBitsAsync(
        DeltaAddress address,
        int bitCount,
        CancellationToken cancellationToken)
    {
        address.ValidateContiguousRange(bitCount, 1, nameof(bitCount));

        long startBit = ((long)address.Index * BitsPerWord) + address.BitIndex!.Value;
        long endBit = startBit + bitCount - 1;
        ushort startWord = checked((ushort)(startBit / BitsPerWord));
        ushort endWord = checked((ushort)(endBit / BitsPerWord));
        int totalWords = endWord - startWord + 1;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        byte[] wordBytes = new byte[totalWords * BytesPerWord];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ushort chunkPdu = checked((ushort)(startWord + chunk.ElementOffset));
                ushort wordCount = checked((ushort)chunk.ElementCount);
                byte[] chunkData = await ReadChunkAsync(session, ModbusArea.HoldingRegister, chunkPdu, wordCount).ConfigureAwait(false);
                Array.Copy(chunkData, 0, wordBytes, chunk.ElementOffset * BytesPerWord, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ExtractBitsFromBigEndianWords(wordBytes, startBit, startWord, bitCount);
    }

    private static byte[] ExtractBitsFromBigEndianWords(byte[] wordBytes, long startBit, ushort startWord, int bitCount)
    {
        byte[] result = new byte[bitCount];
        for (int i = 0; i < bitCount; i++)
        {
            long absoluteBit = startBit + i;
            int wordIndex = (int)((absoluteBit / BitsPerWord) - startWord);
            int bitInWord = (int)(absoluteBit % BitsPerWord);
            ushort wordValue = BinaryBuffer.ReadUInt16BigEndian(wordBytes, wordIndex * BytesPerWord);
            result[i] = ((wordValue >> bitInWord) & 0x01) != 0 ? BitOnValue : BitOffValue;
        }

        return result;
    }

    private async Task WriteWordBitAsync(
        DeltaAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Count > 1)
        {
            throw new ArgumentException("AS 시리즈 D 워드 내 비트 쓰기는 단일 비트(Count == 1)만 지원합니다.", nameof(request));
        }

        bool bitState = request.GetData()[0] != 0;
        int bitIndexInWord = address.BitIndex!.Value;

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            byte[] wordBytes = await ReadChunkAsync(session, ModbusArea.HoldingRegister, address.PduAddress, 1).ConfigureAwait(false);
            ushort currentWord = BinaryBuffer.ReadUInt16BigEndian(wordBytes, 0);
            ushort bitMask = (ushort)(1 << bitIndexInWord);
            ushort updatedWord = bitState ? (ushort)(currentWord | bitMask) : (ushort)(currentWord & ~bitMask);

            byte[] updatedBytes = new byte[BytesPerWord];
            BinaryBuffer.WriteUInt16BigEndian(updatedBytes, 0, updatedWord);
            await WriteChunkAsync(session, address.PduAddress, isBit: false, elementCount: 1, wordsPerElement: 1, updatedBytes).ConfigureAwait(false);

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        ModbusArea area,
        ushort pduAddress,
        ushort quantity)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestPdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);

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
        int elementCount,
        int wordsPerElement,
        byte[] chunkData)
    {
        ushort transactionId = NextTransactionId();
        ushort quantity = checked((ushort)(isBit ? chunkData.Length : chunkData.Length / BytesPerWord));
        byte[] requestPdu = BuildWritePdu(pduAddress, isBit, elementCount, wordsPerElement, quantity, chunkData);

        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);
        ValidateWriteResponse(requestPdu, responsePdu, pduAddress, quantity);
    }

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] pdu, ushort transactionId)
    {
        byte[] requestAdu = WrapAdu(pdu, transactionId);
        await session.SendAsync(requestAdu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding != DeltaTransportEncoding.Tcp)
            {
                return UnwrapAdu(responseAdu, transactionId);
            }

            if (responseAdu.Length >= TcpTransactionIdLength)
            {
                ushort receivedTransactionId = (ushort)((responseAdu[0] << TcpTransactionIdByteShift) | responseAdu[1]);
                if (receivedTransactionId != transactionId)
                {
                    continue;
                }
            }

            return UnwrapAdu(responseAdu, transactionId);
        }
    }

    private byte[] BuildWritePdu(
        ushort pduAddress,
        bool isBit,
        int elementCount,
        int wordsPerElement,
        ushort quantity,
        byte[] chunkData)
    {
        if (isBit)
        {
            if (_options.UseSingleWriteFunctions && elementCount == 1)
            {
                return ModbusRequestBuilder.BuildWriteSingleCoil(pduAddress, chunkData[0] != 0);
            }

            bool[] bits = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                bits[i] = chunkData[i] != 0;
            }

            byte[] packedCoilBytes = BitPacking.Pack(bits);
            return ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, packedCoilBytes);
        }

        if (_options.UseSingleWriteFunctions && elementCount == 1 && wordsPerElement == 1)
        {
            ushort registerValue = BinaryBuffer.ReadUInt16BigEndian(chunkData, 0);
            return ModbusRequestBuilder.BuildWriteSingleRegister(pduAddress, registerValue);
        }

        return ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, chunkData);
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
            return;
        }

        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, chunkPduAddress, expectedQuantity);
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
                ushort pduAddress = checked((ushort)(range.BasePduAddress + (subChunk.ElementOffset * wordsPerElement)));
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
            if (address.Device == "M" && address.Index < DvpRelayBoundaryIndex && (long)address.Index + elementCount > DvpRelayBoundaryIndex)
            {
                int firstElementCount = DvpRelayBoundaryIndex - address.Index;
                ranges.Add(new RangePlan(address.PduAddress, firstElementCount));
                ranges.Add(new RangePlan(DvpHighRelayBasePduAddress, elementCount - firstElementCount));
                return ranges;
            }

            long totalWords = (long)elementCount * wordsPerElement;
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
        lock (_transactionIdLock)
        {
            _transactionId = (ushort)(_transactionId == MaxTransactionId ? InitialTransactionId : _transactionId + 1);
            return _transactionId;
        }
    }

    private void ResetTransactionIdIfNewConnection(long connectionId)
    {
        lock (_transactionIdLock)
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
