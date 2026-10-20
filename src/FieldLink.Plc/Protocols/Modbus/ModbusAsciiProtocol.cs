using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>시리얼 연결 상에서 실행되는 Modbus ASCII 클라이언트 프로토콜 구현체입니다. (사양서 §1, §3)</summary>
public sealed class ModbusAsciiProtocol : IPlcProtocol
{
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly ModbusAsciiOptions _options;
    private readonly ModbusAsciiFrameDecoder _frameDecoder = new();

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public ModbusAsciiProtocol(ITransport transport, ModbusAsciiOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new ModbusAsciiOptions();
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

        var address = ModbusAddress.Parse(request.Address);
        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, request.DataType.GetElementSize() / 2);
        int maxElements = address.IsBit
            ? _options.MaxReadCoils
            : RequestChunker.CalculateElementLimit(_options.MaxReadRegisters, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            foreach (RequestChunker.Chunk chunk in chunks)
            {
                ushort chunkPduAddress = (ushort)(address.PduAddress + (chunk.ElementOffset * wordsPerElement));
                ushort quantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);

                byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, chunkPduAddress, quantity);
                byte[] requestAdu = ModbusRequestBuilder.BuildAsciiAdu(_options.SlaveAddress, pdu);

                byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
                byte[] responsePdu = ModbusResponseParser.UnwrapAsciiAdu(responseAdu, _options.SlaveAddress);

                byte[] chunkData = address.IsBit
                    ? ModbusResponseParser.ParseReadBits(responsePdu, (ModbusFunctionCode)pdu[0], quantity)
                    : ModbusResponseParser.ParseReadRegisters(responsePdu, (ModbusFunctionCode)pdu[0], quantity);

                int destinationOffset = chunk.ElementOffset * request.DataType.GetElementSize();
                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, chunkData.Length);
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

        var address = ModbusAddress.Parse(request.Address);
        if (!address.IsWritable)
        {
            throw new ArgumentException("읽기 전용 영역(이산 입력, 입력 레지스터)에는 쓸 수 없습니다.", nameof(request));
        }

        int wordsPerElement = address.IsBit ? 1 : Math.Max(1, request.DataType.GetElementSize() / 2);
        int maxElements = address.IsBit
            ? _options.MaxWriteCoils
            : RequestChunker.CalculateElementLimit(_options.MaxWriteRegisters, wordsPerElement);

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    ushort chunkPduAddress = (ushort)(address.PduAddress + (chunk.ElementOffset * wordsPerElement));
                    byte[] pdu = BuildWritePdu(address, chunkPduAddress, chunk, request, wordsPerElement);

                    byte[] requestAdu = ModbusRequestBuilder.BuildAsciiAdu(_options.SlaveAddress, pdu);
                    byte[] responseAdu = await session.ExchangeAsync(requestAdu, _frameDecoder).ConfigureAwait(false);
                    byte[] responsePdu = ModbusResponseParser.UnwrapAsciiAdu(responseAdu, _options.SlaveAddress);

                    ValidateWriteResponse(pdu, responsePdu, address, chunkPduAddress, chunk, wordsPerElement);
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

    private byte[] BuildWritePdu(
        ModbusAddress address,
        ushort chunkPduAddress,
        RequestChunker.Chunk chunk,
        PlcWriteRequest request,
        int wordsPerElement)
    {
        if (address.IsBit)
        {
            if (_options.UseSingleWriteFunctions && chunk.ElementCount == 1)
            {
                byte[] raw = request.GetData(chunk.ElementOffset, 1);
                return ModbusRequestBuilder.BuildWriteSingleCoil(chunkPduAddress, raw[0] != 0);
            }

            byte[] bitData = request.GetData(chunk.ElementOffset, chunk.ElementCount);
            bool[] bits = new bool[chunk.ElementCount];
            for (int i = 0; i < chunk.ElementCount; i++)
            {
                bits[i] = bitData[i] != 0;
            }

            byte[] packed = BitPacking.Pack(bits);
            return ModbusRequestBuilder.BuildWriteMultipleCoils(chunkPduAddress, (ushort)chunk.ElementCount, packed);
        }

        if (_options.UseSingleWriteFunctions && chunk.ElementCount == 1 && wordsPerElement == 1)
        {
            byte[] raw = request.GetData(chunk.ElementOffset, 1);
            ushort registerValue = (ushort)((raw[0] << 8) | raw[1]);
            return ModbusRequestBuilder.BuildWriteSingleRegister(chunkPduAddress, registerValue);
        }

        byte[] registerSlice = request.GetData(chunk.ElementOffset, chunk.ElementCount);
        ushort registerCount = (ushort)(chunk.ElementCount * wordsPerElement);
        return ModbusRequestBuilder.BuildWriteMultipleRegisters(chunkPduAddress, registerCount, registerSlice);
    }

    private static void ValidateWriteResponse(
        byte[] requestPdu,
        byte[] responsePdu,
        ModbusAddress address,
        ushort chunkPduAddress,
        RequestChunker.Chunk chunk,
        int wordsPerElement)
    {
        var fc = (ModbusFunctionCode)requestPdu[0];
        if (fc is ModbusFunctionCode.WriteSingleCoil or ModbusFunctionCode.WriteSingleRegister)
        {
            ModbusResponseParser.ParseWriteSingleResponse(responsePdu, requestPdu);
        }
        else
        {
            ushort expectedQuantity = (ushort)(address.IsBit ? chunk.ElementCount : chunk.ElementCount * wordsPerElement);
            ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, fc, chunkPduAddress, expectedQuantity);
        }
    }
}
