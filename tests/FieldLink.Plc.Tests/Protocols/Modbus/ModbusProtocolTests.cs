using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Transport;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Modbus;

public sealed class ModbusProtocolTests
{
    [Fact]
    public async Task ModbusTcp_ReadHoldingRegisters_ReturnsData()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((decoder, sendBytes) =>
        {
            ushort transactionId = (ushort)((sendBytes[0] << 8) | sendBytes[1]);
            return new byte[]
            {
                (byte)(transactionId >> 8), (byte)transactionId, // TxId
                0x00, 0x00,                     // Protocol
                0x00, 0x05,                     // Length
                0x01,                           // UnitId
                0x03,                           // FC
                0x02,                           // ByteCount
                0x12, 0x34                      // Data
            };
        });

        var transport = new MockTransport(mockSession);
        var protocol = new ModbusTcpProtocol(transport);

        var request = new PlcReadRequest("40001", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
    }

    [Fact]
    public async Task ModbusTcp_LateResponseDiscard_AcceptsMatchingTxId()
    {
        var mockSession = new MockTransportSession();
        // 첫 번째 수신: 이전 늦은 응답 (TxId 0x0000)
        mockSession.EnqueueDirectReceive(new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x01, 0x03, 0x02, 0x99, 0x99
        });
        // 두 번째 수신: 일치하는 응답 (TxId 0x0001)
        mockSession.EnqueueDirectReceive(new byte[]
        {
            0x00, 0x01, 0x00, 0x00, 0x00, 0x05, 0x01, 0x03, 0x02, 0x12, 0x34
        });

        var transport = new MockTransport(mockSession);
        var protocol = new ModbusTcpProtocol(transport);

        var request = new PlcReadRequest("40001", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
    }

    [Fact]
    public async Task ModbusTcp_ChunkedRead_CombinesChunks()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, _) => MakeTcpReadResponse(1, 125));
        mockSession.EnqueueReceive((_, _) => MakeTcpReadResponse(2, 25));

        var transport = new MockTransport(mockSession);
        var protocol = new ModbusTcpProtocol(transport);

        var request = new PlcReadRequest("40001", PlcDataType.Int16, 150);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(300, result.Length);
        Assert.Equal(2, mockSession.SentBuffers.Count);
    }

    [Fact]
    public async Task ModbusTcp_WriteSingleRegister_SendsExpectedPdu()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, sendBytes) => (byte[])sendBytes.Clone());

        var transport = new MockTransport(mockSession);
        var options = new ModbusTcpOptions { UseSingleWriteFunctions = true };
        var protocol = new ModbusTcpProtocol(transport, options);

        var request = new PlcWriteRequest("40001", PlcDataType.Int16, new byte[] { 0x12, 0x34 });
        await protocol.WriteAsync(request, CancellationToken.None);

        Assert.Single(mockSession.SentBuffers);
        byte[] sent = mockSession.SentBuffers[0];
        Assert.Equal(0x06, sent[7]); // FC 0x06
    }

    [Fact]
    public async Task ModbusRtu_ReadCoils_ReturnsParsedBits()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, _) =>
        {
            byte[] pdu = new byte[] { 0x01, 0x01, 0x05 }; // FC 0x01, 1 byte, bits: 1, 0, 1
            return ModbusRequestBuilder.BuildRtuAdu(1, pdu);
        });

        var transport = new MockTransport(mockSession);
        var protocol = new ModbusRtuProtocol(transport);

        var request = new PlcReadRequest("00001", PlcDataType.Boolean, 3);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 0, 1 }, result);
    }

    [Fact]
    public async Task ModbusAscii_ReadHoldingRegisters_ReturnsData()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, _) =>
        {
            byte[] pdu = new byte[] { 0x03, 0x02, 0x56, 0x78 };
            return ModbusRequestBuilder.BuildAsciiAdu(1, pdu);
        });

        var transport = new MockTransport(mockSession);
        var protocol = new ModbusAsciiProtocol(transport);

        var request = new PlcReadRequest("40001", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 0x56, 0x78 }, result);
    }

    [Fact]
    public void PlcClientFactory_CreateModbus_ReturnsConfiguredClients()
    {
        var mockTransport = new MockTransport(new MockTransportSession());

        using var tcpClient = PlcClientFactory.CreateModbusTcp(mockTransport);
        Assert.NotNull(tcpClient);

        using var rtuClient = PlcClientFactory.CreateModbusRtu(mockTransport);
        Assert.NotNull(rtuClient);

        using var asciiClient = PlcClientFactory.CreateModbusAscii(mockTransport);
        Assert.NotNull(asciiClient);
    }

    private static byte[] MakeTcpReadResponse(ushort transactionId, int registerCount)
    {
        byte byteCount = (byte)(registerCount * 2);
        var response = new byte[7 + 2 + byteCount];
        response[0] = (byte)(transactionId >> 8);
        response[1] = (byte)transactionId;
        response[4] = (byte)((3 + byteCount) >> 8);
        response[5] = (byte)(3 + byteCount);
        response[6] = 0x01; // UnitId
        response[7] = 0x03; // FC 0x03
        response[8] = byteCount;
        return response;
    }

    private sealed class MockTransport : ITransport
    {
        private readonly ITransportSession _session;

        public MockTransport(ITransportSession session) => _session = session;

        public TransportState State => TransportState.Open;

        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TResult> RunExclusiveAsync<TResult>(
            Func<ITransportSession, Task<TResult>> work,
            CancellationToken cancellationToken = default) => work(_session);

        public void Dispose() { }
    }

    private sealed class MockTransportSession : ITransportSession
    {
        private readonly Queue<Func<IFrameDecoder, byte[], byte[]>> _receiveQueue = new();

        public List<byte[]> SentBuffers { get; } = new();

        public long ConnectionId => 1;

        public CancellationToken CancellationToken => CancellationToken.None;

        public void EnqueueReceive(Func<IFrameDecoder, byte[], byte[]> responder) => _receiveQueue.Enqueue(responder);

        public void EnqueueDirectReceive(byte[] response) => _receiveQueue.Enqueue((_, _) => response);

        public Task SendAsync(byte[] frame)
        {
            SentBuffers.Add(frame);
            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            if (_receiveQueue.Count == 0)
            {
                throw new InvalidOperationException("No mock receive enqueued.");
            }

            byte[] lastSent = SentBuffers.Count > 0 ? SentBuffers[^1] : Array.Empty<byte>();
            Func<IFrameDecoder, byte[], byte[]> responder = _receiveQueue.Dequeue();
            return Task.FromResult(responder(decoder, lastSent));
        }
    }
}
