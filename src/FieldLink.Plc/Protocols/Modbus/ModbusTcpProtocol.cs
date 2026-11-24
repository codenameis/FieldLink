using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>TCP 소켓 상에서 실행되는 Modbus TCP 클라이언트 프로토콜 구현체입니다. (사양서 §1, §3)</summary>
public sealed class ModbusTcpProtocol : IPlcProtocol
{
    private const int BytesPerRegister = 2;
    private const int ByteShift = 8;
    private const ushort MaxTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;
    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: BytesPerRegister);

    private readonly ITransport _transport;
    private readonly ModbusTcpOptions _options;
    private readonly ModbusTcpFrameDecoder _frameDecoder = new();
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public ModbusTcpProtocol(ITransport transport, ModbusTcpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new ModbusTcpOptions();
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
        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndType(address, request.DataType, request.Count, isWrite: false, elementSize);

        int maxElements = address.IsBit
            ? _options.MaxReadCoils
            : (_options.MaxReadRegisters * BytesPerRegister) / elementSize;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            ResetTransactionIdIfNewConnection(session.ConnectionId);

            foreach (RequestChunker.Chunk chunk in chunks)
            {
                int chunkWordOffset = address.IsBit ? chunk.ElementOffset : (chunk.ElementOffset * elementSize) / BytesPerRegister;
                ushort chunkPduAddress = (ushort)(address.PduAddress + chunkWordOffset);
                ushort quantity = address.IsBit
                    ? (ushort)chunk.ElementCount
                    : (ushort)(((chunk.ElementCount * elementSize) + (BytesPerRegister - 1)) / BytesPerRegister);

                byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, chunkPduAddress, quantity);
                byte[] responsePdu = await ExchangeWithLateResponseDiscardAsync(session, pdu).ConfigureAwait(false);

                byte[] chunkData = address.IsBit
                    ? ModbusResponseParser.ParseReadBits(responsePdu, (ModbusFunctionCode)pdu[0], quantity)
                    : ModbusResponseParser.ParseReadRegisters(responsePdu, (ModbusFunctionCode)pdu[0], quantity);

                int destinationOffset = chunk.ElementOffset * elementSize;
                int bytesToCopy = chunk.ElementCount * elementSize;
                if (chunkData.Length < bytesToCopy)
                {
                    throw new PlcException(PlcErrorKind.InvalidResponse, $"응답 바이트 수({chunkData.Length})가 요청 바이트 수({bytesToCopy})보다 적습니다.");
                }

                Array.Copy(chunkData, 0, combinedBuffer, destinationOffset, bytesToCopy);
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

        int elementSize = request.DataType.GetElementSize();
        ValidateAddressAndType(address, request.DataType, request.Count, isWrite: true, elementSize);

        int maxElements = address.IsBit
            ? _options.MaxWriteCoils
            : (_options.MaxWriteRegisters * BytesPerRegister) / elementSize;

        List<RequestChunker.Chunk> chunks = RequestChunker.Plan(request.Count, maxElements);
        int completedElements = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                ResetTransactionIdIfNewConnection(session.ConnectionId);

                foreach (RequestChunker.Chunk chunk in chunks)
                {
                    int chunkWordOffset = address.IsBit ? chunk.ElementOffset : (chunk.ElementOffset * elementSize) / BytesPerRegister;
                    ushort chunkPduAddress = (ushort)(address.PduAddress + chunkWordOffset);
                    byte[] pdu = BuildWritePdu(address, chunkPduAddress, chunk, request, elementSize);

                    byte[] responsePdu = await ExchangeWithLateResponseDiscardAsync(session, pdu).ConfigureAwait(false);
                    ValidateWriteResponse(pdu, responsePdu, address, chunkPduAddress, chunk, elementSize);

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
        int elementSize)
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

        int registerCount = (chunk.ElementCount * elementSize) / BytesPerRegister;
        if (_options.UseSingleWriteFunctions && registerCount == 1)
        {
            byte[] raw = request.GetData(chunk.ElementOffset, chunk.ElementCount);
            ushort registerValue = (ushort)((raw[0] << ByteShift) | raw[1]);
            return ModbusRequestBuilder.BuildWriteSingleRegister(chunkPduAddress, registerValue);
        }

        byte[] registerSlice = request.GetData(chunk.ElementOffset, chunk.ElementCount);
        return ModbusRequestBuilder.BuildWriteMultipleRegisters(chunkPduAddress, (ushort)registerCount, registerSlice);
    }

    private static void ValidateWriteResponse(
        byte[] requestPdu,
        byte[] responsePdu,
        ModbusAddress address,
        ushort chunkPduAddress,
        RequestChunker.Chunk chunk,
        int elementSize)
    {
        var fc = (ModbusFunctionCode)requestPdu[0];
        if (fc is ModbusFunctionCode.WriteSingleCoil or ModbusFunctionCode.WriteSingleRegister)
        {
            ModbusResponseParser.ParseWriteSingleResponse(responsePdu, requestPdu);
        }
        else
        {
            ushort expectedQuantity = address.IsBit
                ? (ushort)chunk.ElementCount
                : (ushort)((chunk.ElementCount * elementSize) / BytesPerRegister);
            ModbusResponseParser.ParseWriteMultipleResponse(responsePdu, fc, chunkPduAddress, expectedQuantity);
        }
    }

    private static void ValidateAddressAndType(ModbusAddress address, PlcDataType dataType, int count, bool isWrite, int elementSize)
    {
        if (address.IsBit && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException("비트 영역(코일/이산 입력)에는 Boolean 자료형만 사용할 수 있습니다.", nameof(dataType));
        }

        if (!address.IsBit && dataType == PlcDataType.Boolean)
        {
            throw new ArgumentException("워드 레지스터 영역에는 Boolean 자료형을 직접 사용할 수 없습니다.", nameof(dataType));
        }

        if (isWrite && !address.IsBit && ((long)count * elementSize) % BytesPerRegister != 0)
        {
            throw new ArgumentException("워드 레지스터 영역에는 2바이트 배수 길이의 데이터만 쓸 수 있습니다.", nameof(count));
        }

        long unitSpan = address.IsBit
            ? count
            : (((long)count * elementSize) + (BytesPerRegister - 1)) / BytesPerRegister;
        if ((long)address.PduAddress + unitSpan - 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "요청 범위가 Modbus 16비트 주소 공간(0~65535)을 초과합니다.");
        }
    }

    private async Task<byte[]> ExchangeWithLateResponseDiscardAsync(ITransportSession session, byte[] pdu)
    {
        ushort transactionId = NextTransactionId();
        byte[] requestAdu = ModbusRequestBuilder.BuildTcpAdu(transactionId, _options.UnitId, pdu);

        await session.SendAsync(requestAdu).ConfigureAwait(false);

        // 사양서 §6 & §8-4 E4: 이전 요청의 늦은 응답은 트랜잭션 ID가 다르므로 버리고 계속 수신
        while (true)
        {
            byte[] responseAdu = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            ushort receivedTxId = (ushort)((responseAdu[0] << ByteShift) | responseAdu[1]);

            if (receivedTxId == transactionId)
            {
                return ModbusResponseParser.UnwrapTcpAdu(responseAdu, transactionId, _options.UnitId);
            }
        }
    }

    private ushort NextTransactionId()
    {
        lock (_txIdLock)
        {
            _transactionId = (ushort)(_transactionId == MaxTransactionId ? InitialTransactionId : _transactionId + 1);
            return _transactionId;
        }
    }

    private void ResetTransactionIdIfNewConnection(long connectionId)
    {
        lock (_txIdLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _transactionId = 0;
            }
        }
    }
}
