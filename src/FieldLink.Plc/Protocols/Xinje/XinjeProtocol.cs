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

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 확장 및 표준 Modbus 프로토콜 클라이언트 드라이버입니다. (사양서 Part A, B)</summary>
public sealed class XinjeProtocol : IPlcProtocol
{
    private const int BytesPerWord = 2;
    private const byte StringPaddingByte = 0x00;
    private const byte BitOnValue = 1;
    private const byte BitOffValue = 0;
    private const int ExtendedMaxWords = 120; // 사양서 §A.1.5: 0x20/0x21 최대 120워드
    private const int ExtendedMaxBits = 1920; // 사양서 §A.1.5: 0x1E/0x1F 최대 1920비트
    private const int StandardModbusMaxReadWords = 125;
    private const int StandardModbusMaxWriteWords = 123;
    private const int StandardModbusMaxReadBits = 2000;
    private const int StandardModbusMaxWriteBits = 1968;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: StringPaddingByte, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly XinjeOptions _options;
    private readonly XinjeTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _transactionIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary><see cref="XinjeProtocol"/>의 새 인스턴스를 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="options">XINJE 프로토콜 설정입니다.</param>
    /// <param name="encoding">RTU 또는 TCP 전송 인코딩입니다.</param>
    public XinjeProtocol(
        ITransport transport,
        XinjeOptions? options = null,
        XinjeTransportEncoding encoding = XinjeTransportEncoding.Tcp)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new XinjeOptions();
        _encoding = encoding;

        _frameDecoder = encoding switch
        {
            XinjeTransportEncoding.Rtu => new XinjeRtuFrameDecoder(),
            XinjeTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
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
        var address = XinjeAddress.Parse(request.Address, _options.Series, isBit);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int requestedByteCount = request.ByteCount;
        PlcReadRequest effectiveRequest = address.IsBit ? request : ProtocolExecutionGuard.NormalizeWordReadRequest(request);

        bool useExtended = ShouldUseExtendedProtocol(address);
        byte[] buffer = useExtended
            ? (address.IsBit
                ? await ReadExtendedBitsAsync(address, effectiveRequest.Count, cancellationToken).ConfigureAwait(false)
                : await ReadExtendedWordsAsync(address, effectiveRequest, cancellationToken).ConfigureAwait(false))
            : await ReadStandardModbusAsync(address, effectiveRequest, cancellationToken).ConfigureAwait(false);

        return ProtocolExecutionGuard.TrimReadResult(buffer, requestedByteCount);
    }

    /// <inheritdoc />
    public async Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = XinjeAddress.Parse(request.Address, _options.Series, isBit);
        ProtocolExecutionGuard.ValidateAddressAndDataType(address.IsBit, request.DataType);

        int totalOriginalCount = request.Count;
        int originalElementsPerUnit = 1;
        PlcWriteRequest effectiveRequest = address.IsBit
            ? request
            : ProtocolExecutionGuard.NormalizeWordWriteRequest(request, out originalElementsPerUnit);

        var context = new WriteContext(address, effectiveRequest, originalElementsPerUnit, totalOriginalCount);
        if (ShouldUseExtendedProtocol(address))
        {
            if (address.IsBit)
            {
                await WriteExtendedBitsAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteExtendedWordsAsync(context, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await WriteStandardModbusAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldUseExtendedProtocol(XinjeAddress address) =>
        _options.UseExtendedProtocol &&
        _options.Series == XinjeSeries.XdXl &&
        address.ExtendedDeviceCode.HasValue;

    private async Task<byte[]> ReadExtendedBitsAsync(XinjeAddress address, int totalBits, CancellationToken cancellationToken)
    {
        address.ValidateContiguousRange(totalBits, isStandardModbus: false, nameof(totalBits));

        int maxBits = Math.Min(_options.MaxReadBits, ExtendedMaxBits);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, maxBits);
        byte[] combinedBuffer = new byte[totalBits];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort bitCount = checked((ushort)chunk.ElementCount);
                byte[] requestPdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, bitCount);
                byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);

                int expectedByteCount = BitPacking.GetPackedByteCount(bitCount);
                byte[] packedBytes = XinjeResponseParser.ParseExtendedReadResponse(
                    responsePdu,
                    XinjeRequestBuilder.ExtendedReadBitsFunctionCode,
                    expectedByteCount);

                bool[] unpackedBits = BitPacking.Unpack(packedBytes, bitCount);
                for (int i = 0; i < bitCount; i++)
                {
                    combinedBuffer[chunk.ElementOffset + i] = unpackedBits[i] ? BitOnValue : BitOffValue;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadExtendedWordsAsync(XinjeAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        address.ValidateContiguousRange((long)request.Count * wordsPerElement, isStandardModbus: false, nameof(request));

        int maxWords = Math.Min(_options.MaxReadWords, ExtendedMaxWords);
        int maxElements = RequestChunker.CalculateElementLimit(maxWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));
                byte[] requestPdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, wordCount);
                byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);

                int expectedByteCount = checked(wordCount * BytesPerWord);
                byte[] chunkData = XinjeResponseParser.ParseExtendedReadResponse(
                    responsePdu,
                    XinjeRequestBuilder.ExtendedReadWordsFunctionCode,
                    expectedByteCount);

                int destinationOffset = checked(chunk.ElementOffset * elementSize);
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadStandardModbusAsync(XinjeAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / BytesPerWord);
        long totalUnits = (long)request.Count * wordsPerElement;
        address.ValidateContiguousRange(totalUnits, isStandardModbus: true, nameof(request));
        ValidateStandardModbusContiguousRange(address, totalUnits, nameof(request));

        int maxElements = address.IsBit
            ? Math.Min(_options.MaxReadBits, StandardModbusMaxReadBits)
            : RequestChunker.CalculateElementLimit(Math.Min(_options.MaxReadWords, StandardModbusMaxReadWords), wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ushort chunkPduAddress = address.Offset(checked(chunk.ElementOffset * wordsPerElement)).ToModbusPduAddress();
                ushort quantity = checked((ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement));
                byte[] chunkData = await ReadStandardChunkAsync(session, address.IsBit, chunkPduAddress, quantity).ConfigureAwait(false);

                int destinationOffset = checked(chunk.ElementOffset * elementSize);
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadStandardChunkAsync(ITransportSession session, bool isBit, ushort pduAddress, ushort quantity)
    {
        ModbusArea area = isBit ? ModbusArea.Coil : ModbusArea.HoldingRegister;
        byte[] requestPdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        return isBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, functionCode, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, functionCode, quantity);
    }

    private async Task WriteExtendedBitsAsync(WriteContext context, CancellationToken cancellationToken)
    {
        context.Address.ValidateContiguousRange(context.Request.Count, isStandardModbus: false, nameof(context));

        int maxBits = Math.Min(_options.MaxWriteBits, ExtendedMaxBits);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(context.Request.Count, maxBits);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    XinjeAddress chunkAddress = context.Address.Offset(chunk.ElementOffset);
                    byte[] chunkData = context.Request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    await WriteExtendedBitChunkAsync(session, chunkAddress, chunkData).ConfigureAwait(false);
                    completedElements = context.AdvanceCompleted(completedElements, chunk.ElementCount);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task WriteExtendedBitChunkAsync(ITransportSession session, XinjeAddress chunkAddress, byte[] chunkData)
    {
        ushort bitCount = checked((ushort)chunkData.Length);
        bool[] bits = ConvertToBools(chunkData);
        byte[] requestPdu = XinjeRequestBuilder.BuildExtendedWriteBitPdu(chunkAddress, bitCount, bits);
        byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);

        XinjeResponseParser.VerifyExtendedWriteResponse(
            responsePdu,
            XinjeRequestBuilder.ExtendedWriteBitsFunctionCode,
            chunkAddress,
            bitCount);
    }

    private async Task WriteExtendedWordsAsync(WriteContext context, CancellationToken cancellationToken)
    {
        int elementSize = context.Request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / BytesPerWord);
        context.Address.ValidateContiguousRange((long)context.Request.Count * wordsPerElement, isStandardModbus: false, nameof(context));

        int maxWords = Math.Min(_options.MaxWriteWords, ExtendedMaxWords);
        int maxElements = RequestChunker.CalculateElementLimit(maxWords, wordsPerElement);
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(context.Request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    XinjeAddress chunkAddress = context.Address.Offset(checked(chunk.ElementOffset * wordsPerElement));
                    ushort wordCount = checked((ushort)(chunk.ElementCount * wordsPerElement));
                    byte[] chunkData = context.Request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    byte[] requestPdu = XinjeRequestBuilder.BuildExtendedWriteWordPdu(chunkAddress, wordCount, chunkData);
                    byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);
                    XinjeResponseParser.VerifyExtendedWriteResponse(
                        responsePdu,
                        XinjeRequestBuilder.ExtendedWriteWordsFunctionCode,
                        chunkAddress,
                        wordCount);

                    completedElements = context.AdvanceCompleted(completedElements, chunk.ElementCount);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private async Task WriteStandardModbusAsync(WriteContext context, CancellationToken cancellationToken)
    {
        int elementSize = context.Request.DataType.GetElementSize();
        int wordsPerElement = context.Address.IsBit ? 1 : Math.Max(1, elementSize / BytesPerWord);
        long totalUnits = (long)context.Request.Count * wordsPerElement;
        context.Address.ValidateContiguousRange(totalUnits, isStandardModbus: true, nameof(context));
        ValidateStandardModbusContiguousRange(context.Address, totalUnits, nameof(context));

        int maxElements = context.Address.IsBit
            ? Math.Min(_options.MaxWriteBits, StandardModbusMaxWriteBits)
            : RequestChunker.CalculateElementLimit(Math.Min(_options.MaxWriteWords, StandardModbusMaxWriteWords), wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(context.Request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPduAddress = context.Address.Offset(checked(chunk.ElementOffset * wordsPerElement)).ToModbusPduAddress();
                    byte[] chunkData = context.Request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    await WriteStandardChunkAsync(session, chunkPduAddress, context.Address.IsBit, chunk.ElementCount, wordsPerElement, chunkData).ConfigureAwait(false);
                    completedElements = context.AdvanceCompleted(completedElements, chunk.ElementCount);
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolExecutionGuard.ShouldWrapWriteException(exception, completedElements))
        {
            throw ProtocolExecutionGuard.WrapWriteException(exception, completedElements);
        }
    }

    private static void ValidateStandardModbusContiguousRange(XinjeAddress startAddress, long totalUnits, string paramName)
    {
        ushort startPdu = startAddress.ToModbusPduAddress();
        int offsetCount = checked((int)Math.Max(0, totalUnits - 1));
        ushort endPdu = startAddress.Offset(offsetCount).ToModbusPduAddress();
        if (endPdu - startPdu != offsetCount)
        {
            throw new ArgumentException("표준 Modbus 불연속 주소 경계를 가로지르는 요청은 지원하지 않습니다.", paramName);
        }
    }

    private async Task WriteStandardChunkAsync(
        ITransportSession session,
        ushort pduAddress,
        bool isBit,
        int elementCount,
        int wordsPerElement,
        byte[] chunkData)
    {
        byte[] requestPdu = BuildStandardWritePdu(pduAddress, isBit, elementCount, wordsPerElement, chunkData);
        byte[] responsePdu = await ExchangePduAsync(session, requestPdu).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)requestPdu[0];
        if (functionCode is ModbusFunctionCode.WriteSingleCoil or ModbusFunctionCode.WriteSingleRegister)
        {
            ModbusResponseParser.ParseWriteSingleResponse(responsePdu, requestPdu);
            return;
        }

        ushort expectedQuantity = checked((ushort)(isBit ? chunkData.Length : chunkData.Length / BytesPerWord));
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, pduAddress, expectedQuantity);
    }

    private byte[] BuildStandardWritePdu(
        ushort pduAddress,
        bool isBit,
        int elementCount,
        int wordsPerElement,
        byte[] chunkData)
    {
        if (!isBit)
        {
            if (_options.UseSingleWriteFunctions && elementCount == 1 && wordsPerElement == 1)
            {
                ushort registerValue = BinaryBuffer.ReadUInt16BigEndian(chunkData, 0);
                return ModbusRequestBuilder.BuildWriteSingleRegister(pduAddress, registerValue);
            }

            ushort wordCount = checked((ushort)(chunkData.Length / BytesPerWord));
            return ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, wordCount, chunkData);
        }

        if (chunkData.Length == 1)
        {
            return ModbusRequestBuilder.BuildWriteSingleCoil(pduAddress, chunkData[0] != 0);
        }

        bool[] bits = ConvertToBools(chunkData);
        byte[] packedCoilBytes = BitPacking.Pack(bits);
        return ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, checked((ushort)chunkData.Length), packedCoilBytes);
    }

    private static bool[] ConvertToBools(byte[] rawBitData)
    {
        bool[] bits = new bool[rawBitData.Length];
        for (int i = 0; i < rawBitData.Length; i++)
        {
            bits[i] = rawBitData[i] != 0;
        }

        return bits;
    }

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] requestPdu)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestAdu = XinjeRequestBuilder.WrapAdu(requestPdu, _options.Station, _encoding, transactionId);
        await session.SendAsync(requestAdu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding == XinjeTransportEncoding.Tcp && responseFrame.Length >= TcpTransactionIdLength)
            {
                ushort receivedTransactionId = (ushort)((responseFrame[0] << TcpTransactionIdByteShift) | responseFrame[1]);
                if (receivedTransactionId != transactionId)
                {
                    continue;
                }
            }

            return XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
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

    private readonly record struct WriteContext(
        XinjeAddress Address,
        PlcWriteRequest Request,
        int OriginalElementsPerUnit,
        int TotalOriginalCount)
    {
        public int AdvanceCompleted(int currentCompleted, int chunkElementCount)
        {
            return ProtocolExecutionGuard.AdvanceCompletedCount(
                currentCompleted,
                chunkElementCount,
                OriginalElementsPerUnit,
                TotalOriginalCount);
        }
    }
}
