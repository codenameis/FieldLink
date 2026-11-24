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
    private readonly object _transactionIdLock = new();

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
            InovanceTransportEncoding.Rtu => new InovanceRtuFrameDecoder(),
            InovanceTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "지원하지 않는 인코딩입니다."),
        };

        // 사양서 §2.2: Inovance 기본 32비트 워드 순서는 하위 워드 먼저(CDAB)
        Traits = new PlcProtocolTraits(ByteOrder.BigEndianWordSwap, DefaultStringFormat);
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

        if (address.IsByteAddress)
        {
            return await ReadByteAddressAsync(address, request, cancellationToken).ConfigureAwait(false);
        }

        ValidateWordRequest(address, request.DataType, request.ByteCount, isWrite: false, nameof(request));

        int requestedByteCount = request.ByteCount;
        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPerWord;
        PlcReadRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordReadRequest(request, bytesPerDeviceUnit);
        int elementSize = effectiveRequest.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(address.Is32BitWord ? WordsPer32BitCounter : 1, elementSize / BytesPerWord);
        int devicesPerElement = CalculateDevicesPerElement(address, elementSize, wordsPerElement);

        address.ValidateContiguousRange((long)effectiveRequest.Count * devicesPerElement, nameof(request));

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

        if (address.IsByteAddress)
        {
            await WriteByteAddressAsync(address, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        ValidateWordRequest(address, request.DataType, request.ByteCount, isWrite: true, nameof(request));

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
        int devicesPerElement = CalculateDevicesPerElement(address, elementSize, wordsPerElement);

        address.ValidateContiguousRange((long)effectiveRequest.Count * devicesPerElement, nameof(request));

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

                    await WriteChunkAsync(session, chunkAddress, chunk.ElementCount, wordsPerElement, chunkData).ConfigureAwait(false);
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

    private static int CalculateDevicesPerElement(InovanceAddress address, int elementSize, int wordsPerElement)
    {
        if (address.IsBit)
        {
            return 1;
        }

        return address.Is32BitWord
            ? Math.Max(1, elementSize / BytesPer32BitCounter)
            : wordsPerElement;
    }

    private static void ValidateWordRequest(
        InovanceAddress address,
        PlcDataType dataType,
        int byteCount,
        bool isWrite,
        string paramName)
    {
        if (!address.Is32BitWord)
        {
            return;
        }

        if (dataType is PlcDataType.Int16 or PlcDataType.UInt16 ||
            (isWrite && dataType is (PlcDataType.Byte or PlcDataType.String) && byteCount % BytesPer32BitCounter != 0))
        {
            throw new ArgumentException(
                "32비트 디바이스(AM MD 또는 H3U C200~C255)는 32비트 이상 자료형 또는 쓰기 시 4바이트 배수로만 접근할 수 있습니다.",
                paramName);
        }
    }

    private async Task<byte[]> ReadByteAddressAsync(
        InovanceAddress address,
        PlcReadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DataType is not (PlcDataType.Byte or PlcDataType.String))
        {
            throw new ArgumentException("AM 시리즈 MB 바이트 주소는 Byte 또는 String 자료형만 사용할 수 있습니다.", nameof(request));
        }

        int totalBytes = request.ByteCount;
        address.ValidateContiguousRange(totalBytes, nameof(request));

        int startByte = address.Number;
        int startWord = startByte / BytesPerWord;
        int endWord = (startByte + totalBytes - 1) / BytesPerWord;
        int totalWords = endWord - startWord + 1;

        InovanceAddress baseWordAddress = address.ToByteWordAddress(startWord);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        byte[] wordBuffer = new byte[totalWords * BytesPerWord];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);
            await ReadWordsIntoBufferAsync(session, baseWordAddress, chunks, wordBuffer).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);

        return ExtractMbBytesFromWords(wordBuffer, startByte, startWord, totalBytes);
    }

    private async Task WriteByteAddressAsync(
        InovanceAddress address,
        PlcWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DataType is not (PlcDataType.Byte or PlcDataType.String))
        {
            throw new ArgumentException("AM 시리즈 MB 바이트 주소는 Byte 또는 String 자료형만 사용할 수 있습니다.", nameof(request));
        }

        int totalBytes = request.Count;
        address.ValidateContiguousRange(totalBytes, nameof(request));

        int startByte = address.Number;
        int startWord = startByte / BytesPerWord;
        int endWord = (startByte + totalBytes - 1) / BytesPerWord;
        int totalWords = endWord - startWord + 1;

        InovanceAddress baseWordAddress = address.ToByteWordAddress(startWord);
        byte[] byteValues = request.GetData();
        List<RequestChunker.Chunk> readChunks = RequestChunker.Plan(totalWords, _options.MaxReadWords);
        List<RequestChunker.Chunk> writeChunks = RequestChunker.Plan(totalWords, _options.MaxWriteWords);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                byte[] wordBuffer = new byte[totalWords * BytesPerWord];
                await ReadWordsIntoBufferAsync(session, baseWordAddress, readChunks, wordBuffer).ConfigureAwait(false);
                ApplyMbBytesToWords(wordBuffer, startByte, startWord, byteValues);

                foreach (RequestChunker.Chunk chunk in writeChunks)
                {
                    InovanceAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset);
                    byte[] chunkData = new byte[chunk.ElementCount * BytesPerWord];
                    Buffer.BlockCopy(wordBuffer, chunk.ElementOffset * BytesPerWord, chunkData, 0, chunkData.Length);

                    await WriteChunkAsync(session, chunkAddress, chunk.ElementCount, wordsPerElement: 1, chunkData).ConfigureAwait(false);
                    long coveredEndByteExclusive = ((long)startWord + chunk.ElementOffset + chunk.ElementCount) * BytesPerWord;
                    completedElements = (int)Math.Min(totalBytes, coveredEndByteExclusive - startByte);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task ReadWordsIntoBufferAsync(
        ITransportSession session,
        InovanceAddress baseWordAddress,
        List<RequestChunker.Chunk> chunks,
        byte[] wordBuffer)
    {
        foreach (RequestChunker.Chunk chunk in chunks)
        {
            InovanceAddress chunkAddress = baseWordAddress.Offset(chunk.ElementOffset);
            ushort wordCount = checked((ushort)chunk.ElementCount);
            byte[] chunkData = await ReadChunkAsync(session, chunkAddress, wordCount).ConfigureAwait(false);
            Array.Copy(chunkData, 0, wordBuffer, chunk.ElementOffset * BytesPerWord, chunkData.Length);
        }
    }

    private static byte[] ExtractMbBytesFromWords(byte[] wordBuffer, int startByte, int startWord, int totalBytes)
    {
        // 사양서 §2.3: 와이어 빅엔디언 워드에서 짝수 MB는 바이트 1(하위), 홀수 MB는 바이트 0(상위)
        byte[] result = new byte[totalBytes];
        for (int i = 0; i < totalBytes; i++)
        {
            int byteIndex = startByte + i;
            int wordOffset = (byteIndex / BytesPerWord) - startWord;
            int byteInWireWord = (byteIndex % BytesPerWord == 0) ? 1 : 0;
            result[i] = wordBuffer[(wordOffset * BytesPerWord) + byteInWireWord];
        }

        return result;
    }

    private static void ApplyMbBytesToWords(byte[] wordBuffer, int startByte, int startWord, byte[] byteValues)
    {
        for (int i = 0; i < byteValues.Length; i++)
        {
            int byteIndex = startByte + i;
            int wordOffset = (byteIndex / BytesPerWord) - startWord;
            int byteInWireWord = (byteIndex % BytesPerWord == 0) ? 1 : 0;
            wordBuffer[(wordOffset * BytesPerWord) + byteInWireWord] = byteValues[i];
        }
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        InovanceAddress chunkAddress,
        ushort quantity)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestPdu = InovanceRequestBuilder.BuildReadPdu(chunkAddress, chunkAddress.PduAddress, quantity);
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
        int elementCount,
        int wordsPerElement,
        byte[] chunkData)
    {
        if (chunkAddress.IsBit && chunkAddress.BitIndexInWord.HasValue)
        {
            await WriteWordBitReadModifyWriteAsync(
                session,
                chunkAddress,
                chunkAddress.BitIndexInWord.Value,
                chunkData[0] != 0).ConfigureAwait(false);
            return;
        }

        ushort transactionId = NextTransactionId();
        ushort quantity = checked((ushort)(chunkAddress.IsBit ? chunkData.Length : chunkData.Length / BytesPerWord));
        byte[] requestPdu = BuildWritePdu(chunkAddress, elementCount, wordsPerElement, quantity, chunkData);

        byte[] responsePdu = await ExchangePduAsync(session, requestPdu, transactionId).ConfigureAwait(false);
        ValidateWriteResponse(requestPdu, responsePdu, chunkAddress.PduAddress, quantity);
    }

    private async Task WriteWordBitReadModifyWriteAsync(
        ITransportSession session,
        InovanceAddress chunkAddress,
        byte bitIndexInWord,
        bool bitState)
    {
        ushort readTransactionId = NextTransactionId();
        byte[] readRequestPdu = InovanceRequestBuilder.BuildReadPdu(chunkAddress, chunkAddress.PduAddress, 1);
        byte[] readResponsePdu = await ExchangePduAsync(session, readRequestPdu, readTransactionId).ConfigureAwait(false);

        byte[] wordBytes = ModbusResponseParser.ParseReadRegisters(
            readResponsePdu,
            (ModbusFunctionCode)readRequestPdu[0],
            1);
        ushort currentWord = BinaryBuffer.ReadUInt16BigEndian(wordBytes, 0);
        ushort bitMask = (ushort)(1 << bitIndexInWord);
        ushort updatedWord = bitState ? (ushort)(currentWord | bitMask) : (ushort)(currentWord & ~bitMask);

        ushort writeTransactionId = NextTransactionId();
        byte[] writeRequestPdu = InovanceRequestBuilder.BuildWriteSingleRegisterPdu(chunkAddress, chunkAddress.PduAddress, updatedWord);
        byte[] writeResponsePdu = await ExchangePduAsync(session, writeRequestPdu, writeTransactionId).ConfigureAwait(false);

        ModbusResponseParser.ParseWriteSingleResponse(writeResponsePdu, writeRequestPdu);
    }

    private byte[] BuildWritePdu(
        InovanceAddress chunkAddress,
        int elementCount,
        int wordsPerElement,
        ushort quantity,
        byte[] chunkData)
    {
        if (chunkAddress.IsBit)
        {
            if (_options.UseSingleWriteFunctions && elementCount == 1)
            {
                return InovanceRequestBuilder.BuildWriteSingleCoilPdu(chunkAddress, chunkAddress.PduAddress, chunkData[0] != 0);
            }

            bool[] bits = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                bits[i] = chunkData[i] != 0;
            }

            byte[] packedCoilBytes = BitPacking.Pack(bits);
            return InovanceRequestBuilder.BuildWriteMultipleCoilsPdu(chunkAddress, chunkAddress.PduAddress, quantity, packedCoilBytes);
        }

        if (_options.UseSingleWriteFunctions && elementCount == 1 && wordsPerElement == 1)
        {
            ushort registerValue = BinaryBuffer.ReadUInt16BigEndian(chunkData, 0);
            return InovanceRequestBuilder.BuildWriteSingleRegisterPdu(chunkAddress, chunkAddress.PduAddress, registerValue);
        }

        return InovanceRequestBuilder.BuildWriteMultipleRegistersPdu(chunkAddress, chunkAddress.PduAddress, quantity, chunkData);
    }

    private static void ValidateWriteResponse(
        byte[] requestPdu,
        byte[] responsePdu,
        ushort chunkPduAddress,
        ushort expectedQuantity)
    {
        byte rawFunctionCode = requestPdu[0];
        if (rawFunctionCode is (byte)ModbusFunctionCode.WriteSingleCoil
            or (byte)ModbusFunctionCode.WriteSingleRegister
            or ((byte)ModbusFunctionCode.WriteSingleCoil + InovanceRequestBuilder.ExtendedFunctionCodeOffset)
            or ((byte)ModbusFunctionCode.WriteSingleRegister + InovanceRequestBuilder.ExtendedFunctionCodeOffset))
        {
            ModbusResponseParser.ParseWriteSingleResponse(responsePdu, requestPdu);
            return;
        }

        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, (ModbusFunctionCode)rawFunctionCode, chunkPduAddress, expectedQuantity);
    }

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] pdu, ushort transactionId)
    {
        byte[] requestAdu = InovanceRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
        await session.SendAsync(requestAdu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding != InovanceTransportEncoding.Tcp)
            {
                return ModbusResponseParser.UnwrapRtuAdu(responseAdu, _options.Station);
            }

            if (responseAdu.Length >= TcpTransactionIdLength)
            {
                ushort receivedTransactionId = (ushort)((responseAdu[0] << TcpTransactionIdByteShift) | responseAdu[1]);
                if (receivedTransactionId != transactionId)
                {
                    continue;
                }
            }

            return ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.Station);
        }
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
}
