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

namespace FieldLink.Plc.Protocols.Megmeet;

/// <summary>Megmeet MC 시리즈 PLC와 통신하는 Modbus 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class MegmeetProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int BytesPer32BitCounter = 4;
    private const int WordsPer32BitCounter = 2;
    private const byte StringPaddingByte = 0x00;
    private const int NoSplitBoundary = -1;
    private const int RelayBoundaryNumber = 2048;
    private const int SpecialRelayBoundaryNumber = 256;
    private const int StepRelayBoundaryNumber = 1024;
    private const int TimerBoundaryNumber = 256;
    private const int CounterBitBoundaryNumber = 256;
    private const int Counter32BitStartNumber = 200;
    private const int CounterExtendedBoundaryNumber = 256;
    private const int SpecialRegisterBoundaryNumber = 256;

    private const ushort ExtendedRelayBasePduAddress = 12000;
    private const ushort ExtendedSpecialRelayBasePduAddress = 30000;
    private const ushort ExtendedStepRelayBasePduAddress = 31000;
    private const ushort ExtendedTimerBasePduAddress = 11000;
    private const ushort ExtendedCounterBitBasePduAddress = 10000;
    private const ushort Counter32BitWordBasePduAddress = 9700;
    private const ushort ExtendedCounter32BitWordBasePduAddress = 10000;
    private const ushort ExtendedSpecialRegisterBasePduAddress = 12000;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly MegmeetOptions _options;
    private readonly MegmeetTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="MegmeetProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">Megmeet 프로토콜 옵션입니다.</param>
    /// <param name="encoding">Modbus 전송 프레임 인코딩 방식입니다.</param>
    public MegmeetProtocol(
        ITransport transport,
        MegmeetOptions? options = null,
        MegmeetTransportEncoding encoding = MegmeetTransportEncoding.Tcp)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new MegmeetOptions();
        _encoding = encoding;

        _frameDecoder = encoding switch
        {
            MegmeetTransportEncoding.Rtu => new ModbusRtuFrameDecoder(),
            MegmeetTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = MegmeetAddress.Parse(request.Address, isBit);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitCounterDataType(address, request.DataType, request.ByteCount, nameof(request));

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPerWord;
        PlcReadRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);

        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = CalculateWordsPerElement(address, elementSize);
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = MegmeetAddress.Parse(request.Address, isBit);
        if (address.Area is ModbusArea.DiscreteInput or ModbusArea.InputRegister)
        {
            throw new ArgumentException($"쓰기 불가능한 영역입니다: {request.Address}", nameof(request));
        }

        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        Validate32BitCounterDataType(address, request.DataType, request.ByteCount, nameof(request));

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPerWord;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit, bytesPerDeviceUnit);

        int wordsPerElement = CalculateWordsPerElement(address, effectiveRequest.DataType.GetElementSize());
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

    private static void Validate32BitCounterDataType(
        MegmeetAddress address,
        PlcDataType dataType,
        int byteCount,
        string paramName)
    {
        if (!address.Is32BitWord)
        {
            return;
        }

        if (dataType is PlcDataType.Int16 or PlcDataType.UInt16 ||
            (dataType is (PlcDataType.Byte or PlcDataType.String) && byteCount % BytesPer32BitCounter != 0))
        {
            throw new ArgumentException(
                "32비트 카운터(C200~C306)는 32비트 이상 자료형 또는 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private static int CalculateWordsPerElement(MegmeetAddress address, int elementByteSize)
    {
        if (address.IsBit)
        {
            return 1;
        }

        if (address.Is32BitWord)
        {
            return Math.Max(WordsPer32BitCounter, elementByteSize / BytesPerWord);
        }

        return Math.Max(1, elementByteSize / BytesPerWord);
    }

    private static List<PlannedChunk> PlanChunks(
        MegmeetAddress address,
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

    private static List<RangePlan> PlanRanges(MegmeetAddress address, int elementCount, int wordsPerElement)
    {
        var ranges = new List<RangePlan>(2);
        int boundary = GetBoundary(address);
        int numbersPerElement = address.Is32BitWord
            ? Math.Max(1, wordsPerElement / WordsPer32BitCounter)
            : wordsPerElement;
        int totalNumberSpan = checked(elementCount * numbersPerElement);

        if (!address.IsBit &&
            address.Device == "C" &&
            address.Number < Counter32BitStartNumber &&
            address.Number + totalNumberSpan > Counter32BitStartNumber)
        {
            throw new ArgumentException(
                "16비트 카운터(C0~C199)와 32비트 카운터(C200~C306) 경계를 가로지르는 요청은 지원하지 않습니다.",
                nameof(address));
        }

        if (boundary > 0 && address.Number < boundary && address.Number + totalNumberSpan > boundary)
        {
            if ((boundary - address.Number) % numbersPerElement != 0)
            {
                throw new ArgumentException(
                    $"다중 워드 원소가 불연속 주소 경계({boundary})에 걸쳐 있습니다.",
                    nameof(address));
            }

            int firstElementCount = (boundary - address.Number) / numbersPerElement;
            if (firstElementCount > 0 && firstElementCount < elementCount)
            {
                ushort secondBasePduAddress = GetSecondBasePdu(address.Device, address.IsBit, boundary);
                ranges.Add(new RangePlan(address.PduAddress, firstElementCount));
                ranges.Add(new RangePlan(secondBasePduAddress, elementCount - firstElementCount));
                return ranges;
            }
        }

        ranges.Add(new RangePlan(address.PduAddress, elementCount));
        return ranges;
    }

    private static int GetBoundary(MegmeetAddress address) => address.Device switch
    {
        "M" => RelayBoundaryNumber,
        "SM" => SpecialRelayBoundaryNumber,
        "S" => StepRelayBoundaryNumber,
        "T" => TimerBoundaryNumber,
        "C" => address.IsBit
            ? CounterBitBoundaryNumber
            : (address.Number < Counter32BitStartNumber ? Counter32BitStartNumber : CounterExtendedBoundaryNumber),
        "SD" => SpecialRegisterBoundaryNumber,
        _ => NoSplitBoundary,
    };

    private static ushort GetSecondBasePdu(string devicePrefix, bool isBit, int boundary) => devicePrefix switch
    {
        "M" => ExtendedRelayBasePduAddress,
        "SM" => ExtendedSpecialRelayBasePduAddress,
        "S" => ExtendedStepRelayBasePduAddress,
        "T" => ExtendedTimerBasePduAddress,
        "C" => isBit
            ? ExtendedCounterBitBasePduAddress
            : (boundary == Counter32BitStartNumber ? Counter32BitWordBasePduAddress : ExtendedCounter32BitWordBasePduAddress),
        "SD" => ExtendedSpecialRegisterBasePduAddress,
        _ => 0,
    };

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
        byte[] chunkData)
    {
        ushort transactionId = NextTransactionId();
        ushort quantity = checked((ushort)(isBit ? chunkData.Length : chunkData.Length / BytesPerWord));
        byte[] requestPdu = BuildWritePdu(pduAddress, quantity, isBit, chunkData);

        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, pduAddress, quantity);
    }

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] pdu, ushort transactionId)
    {
        byte[] requestAdu = WrapAdu(pdu, transactionId);
        await session.SendAsync(requestAdu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding != MegmeetTransportEncoding.Tcp)
            {
                return UnwrapAdu(responseAdu, transactionId);
            }

            if (responseAdu.Length >= TcpTransactionIdLength)
            {
                ushort receivedTxId = (ushort)((responseAdu[0] << TcpTransactionIdByteShift) | responseAdu[1]);
                if (receivedTxId != transactionId)
                {
                    continue;
                }
            }

            return UnwrapAdu(responseAdu, transactionId);
        }
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
        MegmeetTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
        MegmeetTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
        _ => throw new InvalidOperationException(),
    };

    private byte[] UnwrapAdu(byte[] responseAdu, ushort transactionId) => _encoding switch
    {
        MegmeetTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(responseAdu, _options.Station),
        MegmeetTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.Station),
        _ => throw new InvalidOperationException(),
    };

    private ushort NextTransactionId()
    {
        lock (_txIdLock)
        {
            _transactionId = (ushort)(_transactionId == MaxTransactionId ? InitialTransactionId : _transactionId + 1);
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
