using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;
using FieldLink.Transport;
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

        public Func<PlcReadRequest, Task<byte[]>>? ReadHandler { get; set; }

        public Func<PlcWriteRequest, Task>? WriteHandler { get; set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            IsConnectCalled = true;
            return Task.CompletedTask;
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
}
