using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>TCP 위에서 실행되는 Siemens S7 통신(S7comm) 클라이언트입니다. 바이트·비트 읽기와 쓰기를 지원합니다. (사양서 §1, §3, §6)</summary>
/// <remarks>실장비 검증 전입니다. 연결·협상 상태는 연결 번호와 함께 저장하고 연결이 바뀌면 다시 협상합니다.</remarks>
public sealed class S7CommProtocol : IPlcProtocol
{
    private const int ReadOverheadBytes = 18;
    private const int WriteOverheadBytes = 28;
    private const int MaxBytesPerRequest = 8191;

    private static readonly PaddedStringFormat DefaultStringFormat = new(paddingByte: 0x00, wordSize: 2);

    private readonly ITransport _transport;
    private readonly S7CommOptions _options;
    private readonly S7CommFrameDecoder _frameDecoder = new();
    private readonly object _sessionLock = new();

    private long _sessionConnectionId = -1;
    private int _negotiatedPduLength;
    private ushort _pduReference;

    /// <summary>전송과 옵션으로 프로토콜 인스턴스를 초기화합니다.</summary>
    public S7CommProtocol(ITransport transport, S7CommOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new S7CommOptions();
        Traits = new PlcProtocolTraits(ByteOrder.BigEndian, DefaultStringFormat);
    }

    /// <inheritdoc />
    public PlcProtocolTraits Traits { get; }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        return _transport.RunExclusiveAsync(
            async session =>
            {
                await EnsureSessionAsync(session).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var address = S7CommAddress.Parse(request.Address);
        ValidateDataType(address, request.DataType);
        byte[] combinedBuffer = new byte[request.ByteCount];

        await _transport.RunExclusiveAsync(async session =>
        {
            await EnsureSessionAsync(session).ConfigureAwait(false);

            foreach (RequestChunker.Chunk chunk in PlanChunks(address, request.ByteCount, request.Count, WriteOrRead.Read))
            {
                S7CommAddress chunkAddress = address.WithOffset(chunk.ElementOffset);
                byte[] values = await ReadChunkAsync(session, chunkAddress, (ushort)chunk.ElementCount).ConfigureAwait(false);
                Array.Copy(values, 0, combinedBuffer, chunk.ElementOffset, values.Length);
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

        var address = S7CommAddress.Parse(request.Address);
        ValidateDataType(address, request.DataType);
        byte[] allData = request.GetData();
        int completedUnits = 0;

        try
        {
            await _transport.RunExclusiveAsync(async session =>
            {
                await EnsureSessionAsync(session).ConfigureAwait(false);

                foreach (RequestChunker.Chunk chunk in PlanChunks(address, request.ByteCount, request.Count, WriteOrRead.Write))
                {
                    byte[] slice = new byte[chunk.ElementCount];
                    Array.Copy(allData, chunk.ElementOffset, slice, 0, slice.Length);

                    await WriteChunkAsync(session, address.WithOffset(chunk.ElementOffset), slice).ConfigureAwait(false);
                    completedUnits += chunk.ElementCount;
                }

                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (PlcDeviceException exception)
        {
            throw new PlcDeviceException(exception.DeviceErrorCode, exception.Message)
            {
                CompletedCount = ToElementCount(address, request.DataType, completedUnits)
            };
        }
        catch (PlcException exception)
        {
            throw new PlcException(exception.Kind, exception.Message, exception.InnerException)
            {
                CompletedCount = ToElementCount(address, request.DataType, completedUnits)
            };
        }
    }

    private static void ValidateDataType(S7CommAddress address, PlcDataType dataType)
    {
        if (address.IsBit && dataType != PlcDataType.Boolean)
        {
            throw new ArgumentException($"비트 주소 '{address}'는 Boolean 자료형만 읽고 쓸 수 있습니다.", nameof(dataType));
        }

        if (!address.IsBit && dataType == PlcDataType.Boolean)
        {
            throw new ArgumentException($"바이트 단위 주소 '{address}'에는 Boolean을 쓸 수 없습니다. 비트 주소(예: M10.0)를 사용하세요.", nameof(dataType));
        }
    }

    private static int ToElementCount(S7CommAddress address, PlcDataType dataType, int completedUnits)
    {
        return address.IsBit ? completedUnits : completedUnits / dataType.GetElementSize();
    }

    private IReadOnlyList<RequestChunker.Chunk> PlanChunks(S7CommAddress address, int byteCount, int elementCount, WriteOrRead direction)
    {
        if (address.IsBit)
        {
            // 사양서 §4: 비트 접근은 요청 하나에 비트 하나만 담는다.
            return RequestChunker.Plan(elementCount, 1);
        }

        int overhead = direction == WriteOrRead.Read ? ReadOverheadBytes : WriteOverheadBytes;
        int maxBytes = Math.Min(_negotiatedPduLength - overhead, MaxBytesPerRequest);
        return RequestChunker.Plan(byteCount, maxBytes);
    }

    private async Task<byte[]> ReadChunkAsync(ITransportSession session, S7CommAddress address, ushort count)
    {
        ushort pduReference = NextPduReference();
        await session.SendAsync(S7CommRequestBuilder.BuildReadRequest(pduReference, address, count)).ConfigureAwait(false);
        byte[] response = await ReceiveMatchingResponseAsync(session, pduReference).ConfigureAwait(false);

        return S7CommResponseParser.ParseReadResponse(response, pduReference, address, count);
    }

    private async Task WriteChunkAsync(ITransportSession session, S7CommAddress address, byte[] data)
    {
        ushort pduReference = NextPduReference();
        await session.SendAsync(S7CommRequestBuilder.BuildWriteRequest(pduReference, address, data)).ConfigureAwait(false);
        byte[] response = await ReceiveMatchingResponseAsync(session, pduReference).ConfigureAwait(false);

        S7CommResponseParser.ParseWriteResponse(response, pduReference);
    }

    private async Task EnsureSessionAsync(ITransportSession session)
    {
        lock (_sessionLock)
        {
            if (_sessionConnectionId == session.ConnectionId)
            {
                return;
            }
        }

        await session.SendAsync(S7CommRequestBuilder.BuildConnectionRequest(_options)).ConfigureAwait(false);
        S7CommResponseParser.ParseConnectionConfirm(await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false));

        ushort pduReference = ResetPduReference();
        await session.SendAsync(S7CommRequestBuilder.BuildSetupCommunicationRequest(_options, pduReference)).ConfigureAwait(false);
        byte[] response = await ReceiveMatchingResponseAsync(session, pduReference).ConfigureAwait(false);
        int negotiatedPduLength = S7CommResponseParser.ParseSetupCommunication(response, pduReference);

        lock (_sessionLock)
        {
            _negotiatedPduLength = negotiatedPduLength;
            _sessionConnectionId = session.ConnectionId;
        }
    }

    private async Task<byte[]> ReceiveMatchingResponseAsync(ITransportSession session, ushort pduReference)
    {
        // 이전 요청의 늦은 응답은 PDU 참조가 달라 버리고 계속 기다린다.
        while (true)
        {
            byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            if (S7CommResponseParser.ReadPduReference(response) == pduReference)
            {
                return response;
            }
        }
    }

    private ushort NextPduReference()
    {
        lock (_sessionLock)
        {
            _pduReference = (ushort)(_pduReference == ushort.MaxValue ? 1 : _pduReference + 1);
            return _pduReference;
        }
    }

    private ushort ResetPduReference()
    {
        lock (_sessionLock)
        {
            _pduReference = 1;
            return _pduReference;
        }
    }

    private enum WriteOrRead
    {
        Read,
        Write,
    }
}
