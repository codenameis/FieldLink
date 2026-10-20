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
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly InovanceOptions _options;
    private readonly InovanceTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private ushort _transactionId;

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
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        int maxElements = address.IsBit
            ? _options.MaxReadBits
            : RequestChunker.CalculateElementLimit(_options.MaxReadWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                InovanceAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

                byte[] chunkData = await ReadChunkAsync(session, chunkAddress.Area, chunkAddress.PduAddress, quantity, address.IsBit).ConfigureAwait(false);
                int destOffset = chunk.ElementOffset * (address.IsBit ? 1 : elementSize);
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

        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, elementSize / 2);
        int maxElements = address.IsBit
            ? _options.MaxWriteBits
            : RequestChunker.CalculateElementLimit(_options.MaxWriteWords, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                InovanceAddress chunkAddress = address.Offset(chunk.ElementOffset * wordsPerElement);
                ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
                byte[] chunkData = request.GetData(chunk.ElementOffset, chunk.ElementCount);

                await WriteChunkAsync(session, chunkAddress.PduAddress, quantity, address.IsBit, chunkData).ConfigureAwait(false);
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
}
