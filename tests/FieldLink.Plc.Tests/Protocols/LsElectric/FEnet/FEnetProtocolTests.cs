using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using FieldLink.Transport;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.LsElectric.FEnet;

public sealed class FEnetProtocolTests
{
    [Fact]
    public async Task ReadAsync_SingleChunk_ReturnsData()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((decoder, sendBytes) =>
        {
            ushort invokeId = (ushort)(sendBytes[14] | (sendBytes[15] << 8));
            return MakeResponseFrame(invokeId, 0x0055, 0, 0, new byte[] { 0x34, 0x12 });
        });

        var transport = new MockTransport(mockSession);
        var protocol = new FEnetProtocol(transport);

        var request = new PlcReadRequest("%DW100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 0x34, 0x12 }, result);
    }

    [Fact]
    public async Task ReadAsync_LateResponseDiscard_AcceptsMatchingInvokeId()
    {
        var mockSession = new MockTransportSession();
        // 첫 번째: 이전 요청의 늦은 응답 (Invoke ID = 0x0000)
        mockSession.EnqueueDirectReceive(MakeResponseFrame(0, 0x0055, 0, 0, new byte[] { 0x99, 0x99 }));
        // 두 번째: 일치하는 응답 (Invoke ID = 0x0001)
        mockSession.EnqueueDirectReceive(MakeResponseFrame(1, 0x0055, 0, 0, new byte[] { 0x78, 0x56 }));

        var transport = new MockTransport(mockSession);
        var protocol = new FEnetProtocol(transport);

        var request = new PlcReadRequest("%DW100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(new byte[] { 0x78, 0x56 }, result);
    }

    [Fact]
    public async Task ReadAsync_Chunked_CombinesResponses()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, sendBytes) =>
        {
            ushort invokeId = (ushort)(sendBytes[14] | (sendBytes[15] << 8));
            return MakeResponseFrame(invokeId, 0x0055, 0, 0, new byte[1400]);
        });
        mockSession.EnqueueReceive((_, sendBytes) =>
        {
            ushort invokeId = (ushort)(sendBytes[14] | (sendBytes[15] << 8));
            return MakeResponseFrame(invokeId, 0x0055, 0, 0, new byte[600]);
        });

        var transport = new MockTransport(mockSession);
        var protocol = new FEnetProtocol(transport);

        // 1000 words = 2000 bytes. MaxReadBytes = 1400 (700 words), chunk 2 = 300 words (600 bytes)
        var request = new PlcReadRequest("%DW100", PlcDataType.Int16, 1000);
        byte[] result = await protocol.ReadAsync(request, CancellationToken.None);

        Assert.Equal(2000, result.Length);
        Assert.Equal(2, mockSession.SentBuffers.Count);
    }

    [Fact]
    public async Task WriteAsync_SendsValidFrame()
    {
        var mockSession = new MockTransportSession();
        mockSession.EnqueueReceive((_, sendBytes) =>
        {
            ushort invokeId = (ushort)(sendBytes[14] | (sendBytes[15] << 8));
            return MakeResponseFrame(invokeId, 0x0059, 0, 0, Array.Empty<byte>());
        });

        var transport = new MockTransport(mockSession);
        var protocol = new FEnetProtocol(transport);

        var request = new PlcWriteRequest("%DW100", PlcDataType.Int16, new byte[] { 0x12, 0x34 });
        await protocol.WriteAsync(request, CancellationToken.None);

        Assert.Single(mockSession.SentBuffers);
    }

    [Fact]
    public void PlcClientFactory_CreateLsElectricFEnet_ReturnsConfiguredClient()
    {
        var mockTransport = new MockTransport(new MockTransportSession());
        using var client = PlcClientFactory.CreateLsElectricFEnet(mockTransport);

        Assert.NotNull(client);
        Assert.Equal(ByteOrder.LittleEndian, client.ByteOrder);
    }

    private static byte[] MakeResponseFrame(ushort invokeId, ushort command, ushort errorState, ushort errorCode, byte[] data)
    {
        ushort instructionLength = (ushort)(command == 0x0055 ? 14 + data.Length : 10);
        byte[] frame = new byte[20 + instructionLength];

        byte[] companyId = Encoding.ASCII.GetBytes("LSIS-XGT");
        Array.Copy(companyId, 0, frame, 0, 8);
        frame[12] = 0xA0;
        frame[13] = 0x11;
        frame[14] = (byte)invokeId;
        frame[15] = (byte)(invokeId >> 8);
        frame[16] = (byte)instructionLength;
        frame[17] = (byte)(instructionLength >> 8);

        byte bcc = 0;
        for (int i = 0; i < 19; i++)
        {
            bcc = (byte)(bcc + frame[i]);
        }

        frame[19] = bcc;

        frame[20] = (byte)command;
        frame[21] = (byte)(command >> 8);
        frame[22] = 0x14;
        frame[23] = 0x00;
        frame[24] = 0x00;
        frame[25] = 0x00;
        frame[26] = (byte)errorState;
        frame[27] = (byte)(errorState >> 8);
        frame[28] = (byte)errorCode;
        frame[29] = (byte)(errorCode >> 8);

        if (command == 0x0055)
        {
            frame[30] = 0x01;
            frame[31] = 0x00;
            frame[32] = (byte)data.Length;
            frame[33] = (byte)(data.Length >> 8);
            Array.Copy(data, 0, frame, 34, data.Length);
        }

        return frame;
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
