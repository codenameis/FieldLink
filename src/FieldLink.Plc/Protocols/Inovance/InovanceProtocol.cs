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
    private const int BitsPerWord = 16;
    private const int BitsPerByte = 8;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly InovanceOptions _options;
    private readonly InovanceTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="InovanceProtocol"/>을 초기화합니다.</summary>
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
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        if (address.IsBit && address.Area == ModbusArea.HoldingRegister)
        {
            return await ReadWordEmbeddedBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false);
        }

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        int maxElements = address.IsBit
            ? _options.MaxReadBits
            : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                InovanceAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress.Area, chunkAddress.PduAddress, quantity, address.IsBit).ConfigureAwait(false);
                int destOffset = chunk.ElementOffset * elementSize;
                Array.Copy(chunkData, 0, combinedBuffer, destOffset, chunkData.Length);
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
        var address = InovanceAddress.Parse(request.Address, _options.Series, isBitAccess);
        if (address.Area is ModbusArea.DiscreteInput or ModbusArea.InputRegister)
        {
            throw new ArgumentException($"읽기 전용 영역입니다: {request.Address}", nameof(request));
        }

        if (address.IsBit && address.Area == ModbusArea.HoldingRegister)
        {
            throw new NotSupportedException($"AM %MX 워드 내 비트 쓰기는 지원하지 않습니다: {request.Address}");
        }

        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        int maxElements = address.IsBit
            ? _options.MaxWriteBits
            : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    InovanceAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                    ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkAddress.PduAddress, quantity, address.IsBit, chunkData).ConfigureAwait(false);
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

    private async Task<byte[]> ReadWordEmbeddedBitsAsync(InovanceAddress address, int bitCount, CancellationToken cancellationToken)
    {
        int startBit = address.BitIndexInWord ?? 0;
        int totalWordSpan = (startBit + bitCount + (BitsPerWord - 1)) / BitsPerWord;
        byte[] result = new byte[bitCount];

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWordSpan, _options.MaxReadWords);
        int processedWords = 0;
        int collectedBits = 0;

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                InovanceAddress chunkAddress = address.Offset(chunk.ElementOffset);
                byte[] registerBytes = await ReadChunkAsync(
                    session,
                    ModbusArea.HoldingRegister,
                    chunkAddress.PduAddress,
                    (ushort)chunk.ElementCount,
                    isBit: false).ConfigureAwait(false);

                for (int w = 0; w < chunk.ElementCount && collectedBits < bitCount; w++)
                {
                    ushort word = (ushort)((registerBytes[w * BytesPerWord] << BitsPerByte) | registerBytes[(w * BytesPerWord) + 1]);
                    int bitStartInWord = processedWords == 0 ? startBit : 0;
                    for (int b = bitStartInWord; b < BitsPerWord && collectedBits < bitCount; b++)
                    {
                        result[collectedBits++] = (word & (1 << b)) != 0 ? (byte)1 : (byte)0;
                    }

                    processedWords++;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        ModbusArea area,
        ushort pduAddress,
        ushort quantity,
        bool isBit)
    {
        ushort transactionId = NextTransactionId();
        byte[] pdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] responsePdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)pdu[0];
        return isBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, functionCode, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, functionCode, quantity);
    }

    private async Task WriteChunkAsync(
        ITransportSession session,
        ushort pduAddress,
        ushort quantity,
        bool isBit,
        byte[] chunkData)
    {
        ushort transactionId = NextTransactionId();
        byte[] payload = isBit ? PackBitBytes(chunkData) : chunkData;
        byte[] pdu = isBit
            ? ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, payload)
            : ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, payload);

        byte[] responsePdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);

        var functionCode = (ModbusFunctionCode)pdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, pduAddress, quantity);
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

    private static byte[] PackBitBytes(byte[] rawBits)
    {
        bool[] bits = new bool[rawBits.Length];
        for (int i = 0; i < rawBits.Length; i++)
        {
            bits[i] = rawBits[i] != 0;
        }

        return BitPacking.Pack(bits);
    }

    private static void ValidateAddressAndDataType(InovanceAddress address, PlcDataType dataType, int elementSize)
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

    private byte[] WrapAdu(byte[] pdu, ushort transactionId) => _encoding switch
    {
        InovanceTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
        InovanceTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
        _ => throw new InvalidOperationException(),
    };

    private byte[] UnwrapAdu(byte[] adu, ushort transactionId) => _encoding switch
    {
        InovanceTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(adu, _options.Station),
        InovanceTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(adu, transactionId, _options.Station),
        _ => throw new InvalidOperationException(),
    };

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
