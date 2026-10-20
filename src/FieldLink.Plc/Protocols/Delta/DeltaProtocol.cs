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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly DeltaOptions _options;
    private readonly DeltaTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private ushort _transactionId;

    /// <summary>전송, 옵션, 인코딩 방식으로 <see cref="DeltaProtocol"/> 인스턴스를 초기화합니다.</summary>
    public DeltaProtocol(
        ITransport transport,
        DeltaOptions? options = null,
        DeltaTransportEncoding encoding = DeltaTransportEncoding.Rtu)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new DeltaOptions();
        _encoding = encoding;

        _frameDecoder = encoding switch
        {
            DeltaTransportEncoding.Rtu => new ModbusRtuFrameDecoder(),
            DeltaTransportEncoding.Ascii => new ModbusAsciiFrameDecoder(),
            DeltaTransportEncoding.Tcp => new ModbusTcpFrameDecoder(),
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

        var address = DeltaAddress.Parse(request.Address, _options.Series);
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        byte[] combinedBuffer = new byte[request.ByteCount];

        List<RangePlan> ranges = PlanRanges(address, request.Count);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            int globalElementOffset = 0;
            foreach (RangePlan range in ranges)
            {
                int maxElements = address.IsBit
                    ? _options.MaxReadBits
                    : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

                List<RequestChunker.Chunk> chunks = RequestChunker.Plan(range.Count, maxElements);
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPdu = (ushort)(range.BasePduAddress + (chunk.ElementOffset * wordsPerElement));
                    ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

                    byte[] chunkData = await ReadChunkAsync(session, address.Area, chunkPdu, quantity, address.IsBit).ConfigureAwait(false);
                    int destOffset = (globalElementOffset + chunk.ElementOffset) * elementSize;
                    Array.Copy(chunkData, 0, combinedBuffer, destOffset, chunkData.Length);
                }

                globalElementOffset += range.Count;
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

        var address = DeltaAddress.Parse(request.Address, _options.Series);
        if (!address.IsWritable)
        {
            throw new InvalidOperationException($"쓰기 불가능한 영역입니다: {request.Address}");
        }

        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        List<RangePlan> ranges = PlanRanges(address, request.Count);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            int globalElementOffset = 0;
            foreach (RangePlan range in ranges)
            {
                int maxElements = address.IsBit
                    ? _options.MaxWriteBits
                    : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

                List<RequestChunker.Chunk> chunks = RequestChunker.Plan(range.Count, maxElements);
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPdu = (ushort)(range.BasePduAddress + (chunk.ElementOffset * wordsPerElement));
                    ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                    byte[] chunkData = request.GetData(globalElementOffset + chunk.ElementOffset, chunk.ElementCount);

                    await WriteChunkAsync(session, chunkPdu, quantity, address.IsBit, chunkData).ConfigureAwait(false);
                }

                globalElementOffset += range.Count;
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadChunkAsync(
        ITransportSession session,
        ModbusArea area,
        ushort pduAddress,
        ushort quantity,
        bool isBit)
    {
        ushort transactionId = unchecked(++_transactionId);
        byte[] pdu = ModbusRequestBuilder.BuildRead(area, pduAddress, quantity);
        byte[] requestAdu = WrapAdu(pdu, transactionId);

        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

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
        ushort transactionId = unchecked(++_transactionId);
        byte[] pdu = isBit
            ? ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, quantity, chunkData)
            : ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, quantity, chunkData);

        byte[] requestAdu = WrapAdu(pdu, transactionId);
        byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
        byte[] responsePdu = UnwrapAdu(responseAdu, transactionId);

        var functionCode = (ModbusFunctionCode)pdu[0];
        ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, functionCode, pduAddress, quantity);
    }

    private byte[] WrapAdu(byte[] pdu, ushort transactionId)
    {
        return _encoding switch
        {
            DeltaTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(_options.Station, pdu),
            DeltaTransportEncoding.Ascii => ModbusRequestBuilder.BuildAsciiAdu(_options.Station, pdu),
            DeltaTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.Station, pdu),
            _ => throw new InvalidOperationException(),
        };
    }

    private byte[] UnwrapAdu(byte[] adu, ushort transactionId)
    {
        return _encoding switch
        {
            DeltaTransportEncoding.Rtu => ModbusResponseParser.UnwrapRtuAdu(adu, _options.Station),
            DeltaTransportEncoding.Ascii => ModbusResponseParser.UnwrapAsciiAdu(adu, _options.Station),
            DeltaTransportEncoding.Tcp => ModbusResponseParser.UnwrapTcpAdu(adu, transactionId, _options.Station),
            _ => throw new InvalidOperationException(),
        };
    }

    private static List<RangePlan> PlanRanges(DeltaAddress address, int totalCount)
    {
        var ranges = new List<RangePlan>(2);

        if (address.Series == DeltaSeries.Dvp)
        {
            if (address.Device == "M" && address.Index < 1536 && address.Index + totalCount > 1536)
            {
                int firstCount = 1536 - address.Index;
                ranges.Add(new RangePlan(address.PduAddress, firstCount));
                ranges.Add(new RangePlan(0xB000, totalCount - firstCount));
                return ranges;
            }

            if (address.Device == "D" && address.Index < 4096 && address.Index + totalCount > 4096)
            {
                int firstCount = 4096 - address.Index;
                ranges.Add(new RangePlan(address.PduAddress, firstCount));
                ranges.Add(new RangePlan(0x9000, totalCount - firstCount));
                return ranges;
            }
        }

        ranges.Add(new RangePlan(address.PduAddress, totalCount));
        return ranges;
    }

    private readonly record struct RangePlan(ushort BasePduAddress, int Count);
}
