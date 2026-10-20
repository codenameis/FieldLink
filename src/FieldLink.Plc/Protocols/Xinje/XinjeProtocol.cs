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
    private const int MaxExtendedWordChunk = 120;
    private const int MaxExtendedBitChunk = 1920;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly XinjeOptions _options;
    private readonly XinjeTransportEncoding _encoding;
    private readonly IFrameDecoder _frameDecoder;
    private int _transactionId;

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

        if (_options.UseExtendedProtocol && _options.Series == XinjeSeries.XdXl)
        {
            return isBit
                ? await ReadExtendedBitsAsync(address, request.Count, cancellationToken).ConfigureAwait(false)
                : await ReadExtendedWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
        }

        return await ReadStandardModbusAsync(address, request, isBit, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadExtendedBitsAsync(XinjeAddress address, int totalBits, CancellationToken cancellationToken)
    {
        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalBits, MaxExtendedBitChunk);
        byte[] result = new byte[totalBits];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, count);
                byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
                byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

                byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
                byte[] packed = XinjeResponseParser.ParseExtendedReadResponse(resPdu, 0x1E);

                for (int i = 0; i < count; i++)
                {
                    bool bit = (packed[i / 8] & (1 << (i % 8))) != 0;
                    result[chunk.ElementOffset + i] = bit ? (byte)1 : (byte)0;
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<byte[]> ReadExtendedWordsAsync(XinjeAddress address, PlcReadRequest request, CancellationToken cancellationToken)
    {
        int bytesPerElement = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, bytesPerElement / 2);
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, MaxExtendedWordChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(chunkAddress, count);
                byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
                byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

                byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
                byte[] chunkData = XinjeResponseParser.ParseExtendedReadResponse(resPdu, 0x20);

                int destOffset = chunk.ElementOffset * 2;
                Array.Copy(chunkData, 0, combinedBuffer, destOffset, chunkData.Length);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);

        return combinedBuffer;
    }

    private async Task<byte[]> ReadStandardModbusAsync(
        XinjeAddress address,
        PlcReadRequest request,
        bool isBit,
        CancellationToken cancellationToken)
    {
        ushort pduAddr = address.ToModbusPduAddress();
        ModbusArea area = isBit ? ModbusArea.Coil : ModbusArea.HoldingRegister;
        int maxChunk = isBit ? 2000 : 120;
        int totalElements = isBit ? request.Count : request.Count * Math.Max(1, request.DataType.GetElementSize() / 2);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalElements, maxChunk);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ushort chunkPdu = (ushort)(pduAddr + chunk.ElementOffset);
                ushort count = (ushort)chunk.ElementCount;
                ushort transactionId = NextTransactionId();

                byte[] pdu = ModbusRequestBuilder.BuildRead(area, chunkPdu, count);
                byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
                byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

                byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);

                if (isBit)
                {
                    byte[] bits = ModbusResponseParser.ParseReadBits(resPdu, (ModbusFunctionCode)pdu[0], count);
                    Array.Copy(bits, 0, combinedBuffer, chunk.ElementOffset, count);
                }
                else
                {
                    byte[] data = ModbusResponseParser.ParseReadRegisters(resPdu, (ModbusFunctionCode)pdu[0], count);
                    Array.Copy(data, 0, combinedBuffer, chunk.ElementOffset * 2, data.Length);
                }
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

        if (_options.UseExtendedProtocol && _options.Series == XinjeSeries.XdXl)
        {
            if (isBit)
            {
                await WriteExtendedBitsAsync(address, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteExtendedWordsAsync(address, request, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await WriteStandardModbusAsync(address, request, isBit, cancellationToken).ConfigureAwait(false);
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

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                bool[] subBools = new bool[chunk.ElementCount];
                Array.Copy(bools, chunk.ElementOffset, subBools, 0, chunk.ElementCount);
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteBitPdu(chunkAddress, (ushort)chunk.ElementCount, subBools);
                byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
                byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

                byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
                XinjeResponseParser.VerifyExtendedWriteResponse(resPdu, 0x1F);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteExtendedWordsAsync(XinjeAddress address, PlcWriteRequest request, CancellationToken cancellationToken)
    {
        int elementSize = request.DataType.GetElementSize();
        int wordsPerElement = Math.Max(1, elementSize / 2);
        int totalWords = request.Count * wordsPerElement;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(totalWords, MaxExtendedWordChunk);

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                XinjeAddress chunkAddress = address.Offset(chunk.ElementOffset);
                byte[] chunkData = request.GetData(chunk.ElementOffset / wordsPerElement, chunk.ElementCount / wordsPerElement);
                ushort transactionId = NextTransactionId();

                byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteWordPdu(chunkAddress, (ushort)chunk.ElementCount, chunkData);
                byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
                byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

                byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
                XinjeResponseParser.VerifyExtendedWriteResponse(resPdu, 0x21);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteStandardModbusAsync(
        XinjeAddress address,
        PlcWriteRequest request,
        bool isBit,
        CancellationToken cancellationToken)
    {
        ushort pduAddr = address.ToModbusPduAddress();

        await _transport.RunExclusiveAsync<bool>(async session =>
        {
            ushort transactionId = NextTransactionId();
            byte[] pdu = isBit
                ? ModbusRequestBuilder.BuildWriteSingleCoil(pduAddr, request.GetData()[0] != 0)
                : ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddr, (ushort)request.Count, request.GetData());

            byte[] adu = XinjeRequestBuilder.WrapAdu(pdu, _options.Station, _encoding, transactionId);
            byte[] responseFrame = await session.ExchangeAsync(adu, _frameDecoder).ConfigureAwait(false);

            byte[] resPdu = XinjeResponseParser.ExtractPdu(responseFrame, _options.Station, _encoding, transactionId);
            if (isBit)
            {
                ModbusResponseParser.ParseWriteSingleResponse(resPdu, pdu);
            }
            else
            {
                ModbusResponseParser.ParseWriteMultipleResponse(resPdu, (ModbusFunctionCode)pdu[0], pduAddr, (ushort)request.Count);
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private ushort NextTransactionId()
    {
        return (ushort)(Interlocked.Increment(ref _transactionId) & 0xFFFF);
    }
}
