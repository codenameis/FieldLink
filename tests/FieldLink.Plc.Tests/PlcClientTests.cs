using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Transport;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Plc.Tests;

public sealed class PlcClientTests
{
    [Fact]
    public async Task OpenAndClose_TransitionsStateCorrectly()
    {
        var transport = new FakeTransport();
        var protocol = new FakeProtocol();
        using var client = new PlcClient(transport, protocol);

        Assert.False(client.IsOpen);

        PlcResult openResult = await client.OpenAsync();
        Assert.True(openResult.IsSuccess);
        Assert.True(client.IsOpen);
        Assert.True(transport.IsOpenCalled);
        Assert.True(protocol.IsConnectCalled);

        PlcResult closeResult = await client.CloseAsync();
        Assert.True(closeResult.IsSuccess);
        Assert.False(client.IsOpen);
        Assert.True(protocol.IsDisconnectCalled);
        Assert.True(transport.IsCloseCalled);
    }

    [Fact]
    public async Task ReadBeforeOpen_ThrowsInvalidOperationException()
    {
        var transport = new FakeTransport();
        var protocol = new FakeProtocol();
        using var client = new PlcClient(transport, protocol);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ReadInt16Async("D100"));
    }

    [Fact]
    public async Task ReadInt16Async_WhenSuccessful_ReturnsParsedValue()
    {
        var transport = new FakeTransport();
        var protocol = new FakeProtocol
        {
            ReadHandler = request =>
            {
                Assert.Equal("40001", request.Address);
                Assert.Equal(PlcDataType.Int16, request.DataType);
                return Task.FromResult(new byte[] { 0x01, 0x00 }); // 256 in BigEndian
            }
        };

        using var client = new PlcClient(transport, protocol);
        await client.OpenAsync();

        PlcResult<short> result = await client.ReadInt16Async("40001");

        Assert.True(result.IsSuccess);
        Assert.Equal(256, result.Value);
    }

    [Fact]
    public async Task WriteInt32Async_WhenSuccessful_PassesBytesToProtocol()
    {
        byte[]? writtenBytes = null;
        var transport = new FakeTransport();
        var protocol = new FakeProtocol
        {
            WriteHandler = request =>
            {
                writtenBytes = request.GetData();
                return Task.CompletedTask;
            }
        };

        using var client = new PlcClient(transport, protocol);
        await client.OpenAsync();

        PlcResult result = await client.WriteInt32Async("D100", 0x12345678);

        Assert.True(result.IsSuccess);
        Assert.NotNull(writtenBytes);
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, writtenBytes);
    }

    [Fact]
    public async Task ProtocolThrowsDeviceException_MapsToDeviceErrorResult()
    {
        var transport = new FakeTransport();
        var protocol = new FakeProtocol
        {
            ReadHandler = _ => throw new PlcDeviceException(0x02, "Illegal data address")
        };

        using var client = new PlcClient(transport, protocol);
        await client.OpenAsync();

        PlcResult<short> result = await client.ReadInt16Async("40001");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PlcErrorKind.DeviceError, result.Error.Kind);
        Assert.Equal(0x02, result.Error.DeviceErrorCode);
    }

    [Fact]
    public async Task TransportThrowsConnectionClosed_MapsToConnectionLostResult()
    {
        var transport = new FakeTransport();
        var protocol = new FakeProtocol
        {
            ReadHandler = _ => throw new TransportException(TransportFailure.ConnectionClosed, "Remote closed")
        };

        using var client = new PlcClient(transport, protocol);
        await client.OpenAsync();

        PlcResult<short> result = await client.ReadInt16Async("40001");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PlcErrorKind.ConnectionLost, result.Error.Kind);
    }

    [Fact]
    public async Task OpenAsync_WhenProtocolConnectFails_ClosesTransportAndAllowsRetry()
    {
        var transport = new FakeTransport();
        int attempts = 0;
        var protocol = new FakeProtocol
        {
            ConnectHandler = _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new PlcException(PlcErrorKind.ConnectionFailed, "Handshake failed");
                }

                return Task.CompletedTask;
            }
        };

        using var client = new PlcClient(transport, protocol);

        PlcResult firstResult = await client.OpenAsync();
        Assert.False(firstResult.IsSuccess);
        Assert.Equal(PlcErrorKind.ConnectionFailed, firstResult.Error!.Kind);
        Assert.False(client.IsOpen);
        Assert.True(transport.IsCloseCalled);
        Assert.Equal(TransportState.Closed, transport.State);

        PlcResult secondResult = await client.OpenAsync();
        Assert.True(secondResult.IsSuccess);
        Assert.True(client.IsOpen);
        Assert.Equal(TransportState.Open, transport.State);
    }

    [Fact]
    public async Task CloseAsync_WhenTransportRemainsOpenButClientNotOpen_ClosesTransport()
    {
        var transport = new FakeTransport { State = TransportState.Open };
        var protocol = new FakeProtocol();
        using var client = new PlcClient(transport, protocol);

        PlcResult result = await client.CloseAsync();

        Assert.True(result.IsSuccess);
        Assert.True(transport.IsCloseCalled);
        Assert.Equal(TransportState.Closed, transport.State);
    }

    [Fact]
    public async Task WriteInt16Async_TimeoutOnSecondChunk_PreservesCompletedCount()
    {
        var session = new DelayingWriteSession();
        var transport = new SessionBackedTransport(session);
        var protocol = new ModbusTcpProtocol(transport, new ModbusTcpOptions { MaxWriteRegisters = 2 });
        var clientOptions = new PlcClientOptions { Timeout = TimeSpan.FromMilliseconds(40) };
        using var client = new PlcClient(transport, protocol, clientOptions);
        await client.OpenAsync();

        PlcResult result = await client.WriteInt16Async("40001", new short[] { 10, 20, 30, 40 });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(PlcErrorKind.Timeout, result.Error.Kind);
        Assert.Equal(2, result.Error.CompletedCount);
    }

    [Fact]
    public void PlcClientOptions_InvalidTimeoutOrNullEncoding_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlcClientOptions { Timeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1) });
        Assert.Throws<ArgumentNullException>(() =>
            new PlcClientOptions { StringEncoding = null! });
    }

    [Fact]
    public void PlcException_NegativeCompletedCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlcException(PlcErrorKind.InvalidResponse, "err") { CompletedCount = -1 });
    }

    private sealed class FakeTransport : ITransport
    {
        public TransportState State { get; set; } = TransportState.Closed;

        public bool IsOpenCalled { get; private set; }

        public bool IsCloseCalled { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            IsOpenCalled = true;
            State = TransportState.Open;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsCloseCalled = true;
            State = TransportState.Closed;
            return Task.CompletedTask;
        }

        public Task<TResult> RunExclusiveAsync<TResult>(
            Func<ITransportSession, Task<TResult>> work,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Dispose()
        {
            State = TransportState.Disposed;
        }
    }

    private sealed class FakeProtocol : IPlcProtocol
    {
        public PlcProtocolTraits Traits { get; } = new(ByteOrder.BigEndian, new PaddedStringFormat());

        public bool IsConnectCalled { get; private set; }

        public bool IsDisconnectCalled { get; private set; }

        public Func<CancellationToken, Task>? ConnectHandler { get; set; }

        public Func<PlcReadRequest, Task<byte[]>>? ReadHandler { get; set; }

        public Func<PlcWriteRequest, Task>? WriteHandler { get; set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            IsConnectCalled = true;
            return ConnectHandler is not null
                ? ConnectHandler(cancellationToken)
                : Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            IsDisconnectCalled = true;
            return Task.CompletedTask;
        }

        public Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken)
        {
            return ReadHandler is not null
                ? ReadHandler(request)
                : Task.FromResult(new byte[request.ByteCount]);
        }

        public Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken)
        {
            return WriteHandler is not null
                ? WriteHandler(request)
                : Task.CompletedTask;
        }
    }

    private sealed class SessionBackedTransport : ITransport
    {
        private readonly DelayingWriteSession _session;

        public SessionBackedTransport(DelayingWriteSession session) => _session = session;

        public TransportState State { get; private set; } = TransportState.Closed;

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            State = TransportState.Open;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            State = TransportState.Closed;
            return Task.CompletedTask;
        }

        public Task<TResult> RunExclusiveAsync<TResult>(
            Func<ITransportSession, Task<TResult>> work,
            CancellationToken cancellationToken = default)
        {
            _session.CancellationToken = cancellationToken;
            return work(_session);
        }

        public void Dispose() => State = TransportState.Disposed;
    }

    private sealed class DelayingWriteSession : ITransportSession
    {
        private readonly Queue<byte[]> _sentFrames = new();

        public long ConnectionId => 1;

        public CancellationToken CancellationToken { get; set; }

        public Task SendAsync(byte[] frame)
        {
            _sentFrames.Enqueue(frame);
            return Task.CompletedTask;
        }

        public async Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            byte[] sent = _sentFrames.Dequeue();
            if (_sentFrames.Count == 0 && sent[1] == 0x02)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken);
            }

            return new byte[]
            {
                sent[0], sent[1], 0x00, 0x00, 0x00, 0x06,
                sent[6], sent[7], sent[8], sent[9], sent[10], sent[11]
            };
        }
    }
}
