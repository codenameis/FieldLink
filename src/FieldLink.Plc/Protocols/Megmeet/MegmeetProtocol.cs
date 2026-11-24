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
    private const int NoSplitBoundary = -1;
    private const int MBoundary = 2048;
    private const int SmBoundary = 256;
    private const int SBoundary = 1024;
    private const int TBoundary = 256;
    private const int CBitBoundary = 256;
    private const int CWordBoundary = 200;
    private const int SdBoundary = 256;
    private const ushort MSecondBase = 12000;
    private const ushort SmSecondBase = 30000;
    private const ushort SSecondBase = 31000;
    private const ushort TSecondBase = 11000;
    private const ushort CBitSecondBase = 10000;
    private const ushort CWordSecondBase = 9700;
    private const ushort SdSecondBase = 12000;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private const int TcpTransactionIdByteShift = 8;
    private const int TcpTransactionIdLength = 2;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerWord);

    private readonly ITransport _transport;
    private readonly MegmeetOptions _options;
    private readonly MegmeetTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private readonly object _txLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="MegmeetProtocol"/>을 초기화합니다.</summary>
    public MegmeetProtocol(
        ITransport transport,
        MegmeetOptions? options = null,
        MegmeetTransportEncoding encoding = MegmeetTransportEncoding.Rtu)
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
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        byte[] combinedBuffer = new byte[request.ByteCount];

        List<RangePlan> ranges = PlanRanges(address, request.Count, wordsPerElement);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            int globalOffset = 0;
            foreach (RangePlan range in ranges)
            {
                int maxElements = address.IsBit
                    ? _options.MaxReadBits
                    : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

                List<RequestChunker.Chunk> chunks = RequestChunker.Plan(range.Count, maxElements);
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPdu = (ushort)(range.BasePdu + (chunk.ElementOffset * wordsPerElement));
                    ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

                    byte[] chunkData = await ReadChunkAsync(session, address.Area, chunkPdu, quantity, address.IsBit).ConfigureAwait(false);
                    int destOffset = (globalOffset + chunk.ElementOffset) * elementSize;
                    Array.Copy(chunkData, 0, combinedBuffer, destOffset, chunkData.Length);
                }

                globalOffset += range.Count;
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
        var address = MegmeetAddress.Parse(request.Address, isBit);
        if (address.Area is ModbusArea.DiscreteInput or ModbusArea.InputRegister)
        {
            throw new ArgumentException($"읽기 전용 영역입니다: {request.Address}", nameof(request));
        }

        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndDataType(address, request.DataType, elementSize);

        int wordsPerElement = address.IsBit ? 1 : elementSize / BytesPerWord;
        List<RangePlan> ranges = PlanRanges(address, request.Count, wordsPerElement);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync<bool>(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                int globalOffset = 0;
                foreach (RangePlan range in ranges)
                {
                    int maxElements = address.IsBit
                        ? _options.MaxWriteBits
                        : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

                    List<RequestChunker.Chunk> chunks = RequestChunker.Plan(range.Count, maxElements);
                    foreach (RequestChunker.Chunk chunk in chunks)
                    {
                        ushort chunkPdu = (ushort)(range.BasePdu + (chunk.ElementOffset * wordsPerElement));
                        ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                        byte[] chunkData = request.GetData(globalOffset + chunk.ElementOffset, chunk.ElementCount);

                        await WriteChunkAsync(session, chunkPdu, quantity, address.IsBit, chunkData).ConfigureAwait(false);
                        completedElements += chunk.ElementCount;
                    }

                    globalOffset += range.Count;
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

    private static List<RangePlan> PlanRanges(MegmeetAddress address, int count, int wordsPerElement)
    {
        var ranges = new List<RangePlan>(2);
        int boundary = GetBoundary(address);
        int totalUnits = count * wordsPerElement;
        if (boundary > 0 && address.Number < boundary && address.Number + totalUnits > boundary)
        {
            int firstUnits = boundary - address.Number;
            if (!address.IsBit && firstUnits % wordsPerElement != 0)
            {
                throw new ArgumentException($"영역 경계({boundary})를 단일 원소가 걸칠 수 없습니다. ({address})", nameof(address));
            }

            int firstCount = firstUnits / wordsPerElement;
            ushort secondBase = GetSecondBasePdu(address.Device, address.IsBit);
            ranges.Add(new RangePlan(address.PduAddress, firstCount));
            ranges.Add(new RangePlan(secondBase, count - firstCount));
            return ranges;
        }

        ranges.Add(new RangePlan(address.PduAddress, count));
        return ranges;
    }

    private static int GetBoundary(MegmeetAddress address) => address.Device switch
    {
        "M" => MBoundary,
        "SM" => SmBoundary,
        "S" => SBoundary,
        "T" => TBoundary,
        "C" => address.IsBit ? CBitBoundary : CWordBoundary,
        "SD" => SdBoundary,
        _ => NoSplitBoundary,
    };

    private static ushort GetSecondBasePdu(string devicePrefix, bool isBit) => devicePrefix switch
    {
        "M" => MSecondBase,
        "SM" => SmSecondBase,
        "S" => SSecondBase,
        "T" => TSecondBase,
        "C" => isBit ? CBitSecondBase : CWordSecondBase,
        "SD" => SdSecondBase,
        _ => 0,
    };

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, ModbusArea area, ushort pduAddress, ushort quantity, bool isBit)
    {
        ushort transactionId = NextTransactionId();
        byte[] pdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] responsePdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);

        var fc = (ModbusFunctionCode)pdu[0];
        return isBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, fc, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, fc, quantity);
    }

    private async Task WriteChunkAsync(ITransportSession session, ushort pduAddress, ushort quantity, bool isBit, byte[] chunkData)
    {
        ushort transactionId = NextTransactionId();
        byte[] payload = isBit ? PackBitBytes(chunkData) : chunkData;
        byte[] pdu = isBit
            ? ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, payload)
            : ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, payload);

        byte[] responsePdu = await ExchangePduAsync(session, pdu, transactionId).ConfigureAwait(false);

        var fc = (ModbusFunctionCode)pdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, fc, pduAddress, quantity);
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

    private static byte[] PackBitBytes(byte[] rawBits)
    {
        bool[] bits = new bool[rawBits.Length];
        for (int i = 0; i < rawBits.Length; i++)
        {
            bits[i] = rawBits[i] != 0;
        }

        return BitPacking.Pack(bits);
    }

    private static void ValidateAddressAndDataType(MegmeetAddress address, PlcDataType dataType, int elementSize)
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
        MegmeetTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
        MegmeetTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
        _ => throw new InvalidOperationException(),
    };

    private byte[] UnwrapAdu(byte[] adu, ushort transactionId) => _encoding switch
    {
        MegmeetTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(adu, _options.Station),
        MegmeetTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(adu, transactionId, _options.Station),
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

    private readonly record struct RangePlan(ushort BasePdu, int Count);
}
