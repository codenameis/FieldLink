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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly MegmeetOptions _options;
    private readonly MegmeetTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private ushort _transactionId;

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
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        byte[] combinedBuffer = new byte[request.ByteCount];

        List<RangePlan> ranges = PlanRanges(address, request.Count);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
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
                    int destOffset = (globalOffset + chunk.ElementOffset) * (address.IsBit ? 1 : elementSize);
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
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);

        List<RangePlan> ranges = PlanRanges(address, request.Count);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
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
                }

                globalOffset += range.Count;
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static List<RangePlan> PlanRanges(MegmeetAddress address, int count)
    {
        var ranges = new List<RangePlan>(2);
        int boundary = GetBoundary(address);
        if (boundary > 0 && address.Number < boundary && address.Number + count > boundary)
        {
            int firstCount = boundary - address.Number;
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
        "M" => 2048,
        "SM" => 256,
        "S" => 1024,
        "T" => 256,
        "C" => address.IsBit ? 256 : 200,
        "SD" => 256,
        _ => -1,
    };

    private static ushort GetSecondBasePdu(string devicePrefix, bool isBit) => devicePrefix switch
    {
        "M" => 12000,
        "SM" => 30000,
        "S" => 31000,
        "T" => 11000,
        "C" => isBit ? (ushort)10000 : (ushort)9700,
        "SD" => 12000,
        _ => 0,
    };

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, ModbusArea area, ushort pduAddress, ushort quantity, bool isBit)
    {
        ushort transactionId = unchecked(++_transactionId);
        byte[] pdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] requestAdu = WrapAdu(pdu, transactionId);

        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

        var fc = (ModbusFunctionCode)pdu[0];
        return isBit
            ? ModbusResponseParser.ParseReadBits(responsePdu, fc, quantity)
            : ModbusResponseParser.ParseReadRegisters(responsePdu, fc, quantity);
    }

    private async Task WriteChunkAsync(ITransportSession session, ushort pduAddress, ushort quantity, bool isBit, byte[] chunkData)
    {
        ushort transactionId = unchecked(++_transactionId);
        byte[] pdu = isBit
            ? ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, chunkData)
            : ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, chunkData);

        byte[] requestAdu = WrapAdu(pdu, transactionId);
        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

        var fc = (ModbusFunctionCode)pdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, fc, pduAddress, quantity);
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

    private readonly record struct RangePlan(ushort BasePdu, int Count);
}
