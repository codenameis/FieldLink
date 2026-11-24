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

namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance AM, H3U, H5U 시리즈 PLC와 통신하는 프로토콜 드라이버입니다. (사양서 §1~§3)</summary>
public sealed class InovanceProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const int BytesPer32BitCounter = 4;
    private const int WordsPer32BitCounter = 2;
    private const int H3uCounter32Start = 200;
    private const int H3uCounter32EndExclusive = 256;
    private const byte StringPaddingByte = 0x00;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly InovanceOptions _options;
    private readonly InovanceTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="InovanceProtocol"/>을 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">Inovance 프로토콜 옵션입니다.</param>
    /// <param name="encoding">Modbus 전송 프레임 인코딩 방식입니다.</param>
    public InovanceProtocol(
        ITransport transport,
        InovanceOptions? options = null,
        InovanceTransportEncoding encoding = InovanceTransportEncoding.Tcp)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new InovanceOptions();
        _encoding = encoding;

        _frameDecoder = encoding switch
        {
            InovanceTransportEncoding.Rtu => new ModbusRtuFrameDecoder(),
            InovanceTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
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
        var address = InovanceAddress.Parse(request.Address, _options.Series, isBitAccess);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        ValidateWordRequest(address, request.DataType, request.ByteCount, nameof(request));

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPerWord;
        PlcReadRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(address.Is32BitWord ? WordsPer32BitCounter : 1, elementSize / BytesPerWord);
        int devicesPerElement = address.IsBit
            ? 1
            : (address.Is32BitWord ? Math.Max(1, elementSize / BytesPer32BitCounter) : wordsPerElement);
        ValidateH3uCounterRange(address, checked(effectiveRequest.Count * devicesPerElement), nameof(request));

        int maxElements = address.BitIndexInWord.HasValue
            ? 1
            : (address.IsBit
                ? _options.MaxReadBits
                : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement));

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElements);
        byte[] combinedBuffer = new byte[effectiveRequest.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                InovanceAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * devicesPerElement));
                ushort quantity = checked((ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement));

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress, quantity).ConfigureAwait(false);
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
        var address = InovanceAddress.Parse(request.Address, _options.Series, isBitAccess);
        if (address.Area is ModbusArea.DiscreteInput or ModbusArea.InputRegister)
        {
            throw new ArgumentException($"쓰기 불가능한 영역입니다: {request.Address}", nameof(request));
        }

        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);
        ValidateWordRequest(address, request.DataType, request.ByteCount, nameof(request));

        if (address.BitIndexInWord.HasValue && request.Count > 1)
        {
            throw new ArgumentException("AM 시리즈 MX 워드 내 비트 쓰기는 단일 비트(Count == 1)만 지원합니다.", nameof(request));
        }

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPerWord;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit, bytesPerDeviceUnit);

        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(address.Is32BitWord ? WordsPer32BitCounter : 1, elementSize / BytesPerWord);
        int devicesPerElement = address.IsBit
            ? 1
            : (address.Is32BitWord ? Math.Max(1, elementSize / BytesPer32BitCounter) : wordsPerElement);
        ValidateH3uCounterRange(address, checked(effectiveRequest.Count * devicesPerElement), nameof(request));

        int maxElements = address.BitIndexInWord.HasValue
            ? 1
            : (address.IsBit
                ? _options.MaxWriteBits
                : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement));

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(effectiveRequest.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    InovanceAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * devicesPerElement));
                    byte[] chunkData = effectiveRequest.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress, chunkData).ConfigureAwait(false);
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

    private static void ValidateWordRequest(
        InovanceAddress address,
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
                "32비트 디바이스(AM MD 또는 H3U C200~C255)는 32비트 이상 자료형 또는 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private static void ValidateH3uCounterRange(InovanceAddress address, int totalDevices, string paramName)
    {
        if (address.Series != InovanceSeries.H3u || address.IsBit || address.DeviceType != "C")
        {
            return;
        }

        if (address.Number < H3uCounter32Start && address.Number + totalDevices > H3uCounter32Start)
        {
            throw new ArgumentException(
                "H3U 16비트 카운터(C0~C199)와 32비트 카운터(C200~C255) 경계를 넘는 요청입니다.",
                paramName);
        }

        if (address.Number >= H3uCounter32Start && address.Number + totalDevices > H3uCounter32EndExclusive)
        {
            throw new ArgumentException(
                "H3U 32비트 카운터(C200~C255) 범위를 초과했습니다.",
                paramName);
        }
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        InovanceAddress chunkAddress,
        ushort quantity)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestPdu = ModbusRequestBuilder.BuildRead(chunkAddress.Area, chunkAddress.PduAddress, quantity);
        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        if (chunkAddress.IsBit && chunkAddress.BitIndexInWord.HasValue)
        {
            byte[] wordBytes = ModbusResponseParser.ParseReadRegisters(responsePdu, functionCode, 1);
            ushort wordValue = BinaryBuffer.ReadUInt16BigEndian(wordBytes, 0);
            bool bitState = ((wordValue >> chunkAddress.BitIndexInWord.Value) & 0x01) != 0;
            return [bitState ? BitOnValue : BitOffValue];
        }

        return chunkAddress.IsBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, functionCode, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, functionCode, quantity);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        InovanceAddress chunkAddress,
        byte[] chunkData)
    {
        if (chunkAddress.IsBit && chunkAddress.BitIndexInWord.HasValue)
        {
            await WriteWordBitReadModifyWriteAsync(
                session,
                chunkAddress.PduAddress,
                chunkAddress.BitIndexInWord.Value,
                chunkData[0] != 0).ConfigureAwait(false);
            return;
        }

        ushort transactionId = NextTransactionId();
        ushort quantity = checked((ushort)(chunkAddress.IsBit ? chunkData.Length : chunkData.Length / BytesPerWord));
        byte[] requestPdu = BuildWritePdu(chunkAddress.PduAddress, quantity, chunkAddress.IsBit, chunkData);

        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, chunkAddress.PduAddress, quantity);
    }

    private async Task WriteWordBitReadModifyWriteAsync(
        ITransportSession session,
        ushort pduAddress,
        byte bitIndexInWord,
        bool bitState)
    {
        ushort readTxId = NextTransactionId();
        byte[] readRequestPdu = ModbusRequestBuilder.BuildRead(ModbusArea.HoldingRegister, pduAddress, 1);
        byte[] readResponsePdu = await ExchangePduAsync(session, readRequestPdu, readTxId).ConfigureAwait(false);

        byte[] wordBytes = ModbusResponseParser.ParseReadRegisters(
            readResponsePdu,
            ModbusFunctionCode.ReadHoldingRegisters,
            1);
        ushort currentWord = BinaryBuffer.ReadUInt16BigEndian(wordBytes, 0);
        ushort bitMask = (ushort)(1 << bitIndexInWord);
        ushort updatedWord = bitState ? (ushort)(currentWord | bitMask) : (ushort)(currentWord & ~bitMask);

        ushort writeTxId = NextTransactionId();
        byte[] writeRequestPdu = ModbusRequestBuilder.BuildWriteSingleRegister(pduAddress, updatedWord);
        byte[] writeResponsePdu = await ExchangePduAsync(session, writeRequestPdu, writeTxId).ConfigureAwait(false);

        ModbusResponseParser.ParseWriteSingleResponse(writeResponsePdu, writeRequestPdu);
    }

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] pdu, ushort transactionId)
    {
        byte[] requestAdu = WrapAdu(pdu, transactionId);
        await session.SendAsync(requestAdu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding != InovanceTransportEncoding.Tcp)
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
        InovanceTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
        InovanceTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
        _ => throw new InvalidOperationException(),
    };

    private byte[] UnwrapAdu(byte[] responseAdu, ushort transactionId) => _encoding switch
    {
        InovanceTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(responseAdu, _options.Station),
        InovanceTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.Station),
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
}
