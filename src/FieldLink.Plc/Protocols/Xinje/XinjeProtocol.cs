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
    private const int BitsPerByte = 8;
    private const int MaxExtendedWordChunk = 120;
    private const int MaxExtendedBitChunk = 1920;
    private const int StandardMaxReadBits = 2000;
    private const int StandardMaxWriteBits = 1968;
    private const int StandardMaxWords = 120;
    private const byte ExtendedReadBitsFc = 0x1E;
    private const byte ExtendedWriteBitsFc = 0x1F;
    private const byte ExtendedReadWordsFc = 0x20;
    private const byte ExtendedWriteWordsFc = 0x21;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly XinjeOptions _options;
    private readonly XinjeTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary><see cref="XinjeProtocol"/>의 새 인스턴스를 초기화합니다.</summary>
    public XinjeProtocol(
        ITransport transport,
        XinjeOptions? options = null,
        XinjeTransportEncoding encoding = XinjeTransportEncoding.Rtu)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new XinjeOptions();
        _encoding = encoding;
        _frameDecoder = encoding == XinjeTransportEncoding.Tcp
            ? new ModbusTcpFrameDecoder()
            : new XinjeRtuFrameDecoder();

        Traits = new PlcProtocolTraits(ByteOrder.BigEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public ITransport Transport => _transport;

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
        int elementSize = request.DataType.GetElementSize();
        ValidateDataType(address, request.DataType, elementSize);

        if (_options.UseExtendedProtocol && _options.Series == XinjeSeries.XdXl)
        {
            return isBit
                ? await ReadExtendedBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false)
                : await ReadExtendedWordsAsync(address, request, elementSize, cancellationToken).ConfigureAwait(false);
        }

        return await ReadStandardModbusAsync(address, request, isBit, elementSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadExtendedBitsAsync(XinjeAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxExtendedBitChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, count);
                byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);
                byte[] packed = XinjeResponseParser.ParseExtendedReadResponse(resPdu, ExtendedReadBitsFc);
                int expectedPackedBytes = BitPacking.GetPackedByteCount(count);
                if (packed.Length != expectedPackedBytes)
                {
                    throw new PlcException(PlcErrorKind.InvalidResponse, $"확장 비트 응답 길이({packed.Length})가 기대({expectedPackedBytes})와 일치하지 않습니다.");
                }

                for (int i = 0; i < count; i++)
                {
                    bool bit = (packed[i / BitsPerByte] & (1 << (i % BitsPerByte))) != 0;
                    result[chunk.ElementOffset + i] = bit ? (byte)1 : (byte)0;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadExtendedWordsAsync(XinjeAddress address, PlcReadRequest request, int elementSize, CancellationToken cancellationToken)
    {
        int wordsPerElement = elementSize / BytesPerWord;
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, MaxExtendedWordChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, count);
                byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);
                byte[] chunkData = XinjeResponseParser.ParseExtendedReadResponse(resPdu, ExtendedReadWordsFc);
                int expectedBytes = count * BytesPerWord;
                if (chunkData.Length != expectedBytes)
                {
                    throw new PlcException(PlcErrorKind.InvalidResponse, $"확장 워드 응답 길이({chunkData.Length})가 기대({expectedBytes})와 일치하지 않습니다.");
                }

                int destOffset = chunk.ElementOffset * BytesPerWord;
                Array.Copy(chunkData, 0, combinedBuffer, destOffset, expectedBytes);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadStandardModbusAsync(
        XinjeAddress address,
        PlcReadRequest request,
        bool isBit,
        int elementSize,
        CancellationToken cancellationToken)
    {
        ushort pduAddr = address.ToModbusPduAddress();
        ModbusArea area = isBit ? ModbusArea.Coil : ModbusArea.HoldingRegister;
        int wordsPerElement = isBit ? 1 : elementSize / BytesPerWord;
        int maxChunk = isBit ? StandardMaxReadBits : RequestChunker.CalculateElementLimit(StandardMaxWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ushort chunkPdu = (ushort)(pduAddr + (chunk.ElementOffset * wordsPerElement));
                ushort quantity = (ushort)(isBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                ushort transactionId = NextTransactionId();

                byte[] pdu = ModbusRequestBuilder.BuildRead(area, chunkPdu, quantity);
                byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);

                byte[] chunkData = isBit
                    ? ModbusResponseParser.ParseReadBits(resPdu, (ModbusFunctionCode)pdu[0], quantity)
                    : ModbusResponseParser.ParseReadRegisters(resPdu, (ModbusFunctionCode)pdu[0], quantity);
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

        bool isBit = request.DataType == PlcDataType.Boolean;
        var address = XinjeAddress.Parse(request.Address, _options.Series, isBit);
        int elementSize = request.DataType.GetElementSize();
        ValidateDataType(address, request.DataType, elementSize);

        if (_options.UseExtendedProtocol && _options.Series == XinjeSeries.XdXl)
        {
            if (isBit)
            {
                await WriteExtendedBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteExtendedWordsAsync(address, request, elementSize, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await WriteStandardModbusAsync(address, request, isBit, elementSize, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteExtendedBitsAsync(XinjeAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int count = request.Count;
        byte[] raw = request.GetData();
        bool[] bools = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bools[i] = raw[i] != 0;
        }

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(count, MaxExtendedBitChunk);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                    bool[] subBools = new bool[chunk.ElementCount];
                    Array.Copy(bools, chunk.ElementOffset, subBools, 0, chunk.ElementCount);
                    ushort transactionId = NextTransactionId();

                    byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteBitPdu(chunkAddress, (ushort)chunk.ElementCount, subBools);
                    byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);
                    XinjeResponseParser.VerifyExtendedWriteResponse(resPdu, ExtendedWriteBitsFc);
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

    private async Task WriteExtendedWordsAsync(XinjeAddress address, PlcWriteRequest request, int elementSize, CancellationToken cancellationToken)
    {
        int wordsPerElement = elementSize / BytesPerWord;
        int maxElements = RequestChunker.CalculateElementLimit(MaxExtendedWordChunk, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    ushort wordCount = (ushort)(chunk.ElementCount * wordsPerElement);
                    ushort transactionId = NextTransactionId();

                    byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteWordPdu(chunkAddress, wordCount, chunkData);
                    byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);
                    XinjeResponseParser.VerifyExtendedWriteResponse(resPdu, ExtendedWriteWordsFc);
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

    private async Task WriteStandardModbusAsync(
        XinjeAddress address,
        PlcWriteRequest request,
        bool isBit,
        int elementSize,
        CancellationToken cancellationToken)
    {
        ushort pduAddr = address.ToModbusPduAddress();
        int wordsPerElement = isBit ? 1 : elementSize / BytesPerWord;
        int maxElements = isBit
            ? StandardMaxWriteBits
            : RequestChunker.CalculateElementLimit(StandardMaxWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPdu = (ushort)(pduAddr + (chunk.ElementOffset * wordsPerElement));
                    ushort quantity = (ushort)(isBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
                    ushort transactionId = NextTransactionId();

                    byte[] pdu;
                    if (isBit && chunk.ElementCount == 1)
                    {
                        pdu = ModbusRequestBuilder.BuildWriteSingleCoil(chunkPdu, chunkData[0] != 0);
                    }
                    else if (isBit)
                    {
                        bool[] bits = new bool[chunk.ElementCount];
                        for (int i = 0; i < chunk.ElementCount; i++)
                        {
                            bits[i] = chunkData[i] != 0;
                        }

                        pdu = ModbusRequestBuilder.BuildWriteMultipleCoils(chunkPdu, quantity, BitPacking.Pack(bits));
                    }
                    else
                    {
                        pdu = ModbusRequestBuilder.BuildWriteMultipleRegisters(chunkPdu, quantity, chunkData);
                    }

                    byte[] resPdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);
                    if (isBit && chunk.ElementCount == 1)
                    {
                        ModbusResponseParser.ParseWriteSingleResponse(resPdu, pdu);
                    }
                    else
                    {
                        ModbusResponseParser.ParseWriteMultipleResponse(resPdu, (ModbusFunctionCode)pdu[0], chunkPdu, quantity);
                    }

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

    private async Task<byte[]> ExchangePduAsync(ITransportSession session, byte[] pdu, ushort transactionId)
    {
        byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
        await session.SendAsync(adu).ConfigureAwait(false);

        while (true)
        {
            byte[] responseFrame = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (_encoding == XinjeTransportEncoding.Tcp && responseFrame.Length >= TcpTransactionIdLength)
            {
                ushort receivedTxId = (ushort)((responseFrame[0] << TcpTransactionIdByteShift) | responseFrame[1]);
                if (receivedTxId != transactionId)
                {
                    continue;
                }
            }

            return XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
        }
    }

    private static void ValidateDataType(XinjeAddress address, PlcDataType dataType, int elementSize)
    {
        if (address.IsBit && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException($"비트 영역 '{address}'에는 Boolean 자료형만 사용할 수 있습니다.", nameof(dataType));
        }

        if (!address.IsBit && elementSize % BytesPerWord != 0)
        {
            throw new ArgumentException($"워드 영역 '{address}'에는 2바이트 배수 자료형만 사용할 수 있습니다.", nameof(dataType));
        }
    }

    private ushort NextTransactionId()
    {
        lock (_txLock)
        {
            _transactionId = (ushort)(_transactionId == MaxTransactionId ? InitialTransactionId : _transactionId + 1);
            return _transactionId;
        }
    }

    private void ResetTransactionIdIfNewConnection(long connectionId)
    {
        lock (_txLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _transactionId = 0;
            }
        }
    }
}
