using System.Collections.Concurrent;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task SerialExplicitOpenAndBufferedFramesAsync()
    {
        var port = new FakeSerialPort();
        using var client = NewSerial(port);
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeAsync([1]));
        TestAssert.Equal(0, port.Writes.Count);
        await client.OpenAsync();
        port.Incoming.Enqueue([10]); port.Incoming.Enqueue([11, 20, 21]);
        TestAssert.Bytes([10, 11], await client.ExchangeAsync([1]));
        TestAssert.Bytes([20, 21], await client.ExchangeAsync([2]));
        TestAssert.Equal(2, port.Writes.Count);
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task SerialTimeoutAndReconnectDiscardOldInputAsync()
    {
        var first = new FakeSerialPort(); var second = new FakeSerialPort();
        var ports = new Queue<FakeSerialPort>([first, second]);
        using var client = new SerialClient(new SerialPortSettings("virtual"), new FixedLengthFrame(2),
            portFactory: _ => ports.Dequeue());
        await client.OpenAsync(); long generation = client.ConnectionGeneration;
        first.Incoming.Enqueue([7]);
        try { await client.ExchangeAsync([1], TimeSpan.FromMilliseconds(70)); throw new Exception("시간 초과가 필요합니다."); }
        catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.Timeout, error.Failure); }
        TestAssert.Equal(ClientState.Faulted, client.State);
        TestAssert.True(first.Disposed);
        await client.OpenAsync();
        TestAssert.True(client.ConnectionGeneration > generation);
        second.Incoming.Enqueue([8, 9]);
        TestAssert.Bytes([8, 9], await client.ExchangeAsync([2]));
    }

    internal static async Task SerialWaitingCancellationKeepsActivePortAsync()
    {
        var port = new FakeSerialPort(); using var client = NewSerial(port);
        await client.OpenAsync();
        var active = client.ExchangeAsync([1]);
        await port.Written.Task;
        using var cancel = new CancellationTokenSource();
        var waiting = client.ExchangeAsync([2], cancellationToken: cancel.Token);
        cancel.Cancel();
        try { await waiting; throw new Exception("취소가 필요합니다."); }
        catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.Cancelled, error.Failure); }
        TestAssert.True(!port.Disposed);
        port.Incoming.Enqueue([3, 4]);
        TestAssert.Bytes([3, 4], await active);
        TestAssert.Equal(1, port.Writes.Count);
    }

    internal static async Task SerialOldRequestsCannotUseNewPortAsync()
    {
        var first = new FakeSerialPort(); var second = new FakeSerialPort();
        var ports = new Queue<FakeSerialPort>([first, second]);
        using var client = new SerialClient(new SerialPortSettings("virtual"), new FixedLengthFrame(2), portFactory: _ => ports.Dequeue());
        await client.OpenAsync();
        var active = client.ExchangeAsync([1]); await first.Written.Task;
        var waiting = client.ExchangeAsync([2]);
        client.Close(); await client.OpenAsync();
        await TestAssert.ThrowsAsync<CommunicationException>(() => active);
        await TestAssert.ThrowsAsync<CommunicationException>(() => waiting);
        TestAssert.Equal(0, second.Writes.Count);
        TestAssert.Equal(ClientState.Open, client.State);
    }

    internal static async Task SerialTransactionLifetimeAndRejectedFrameAsync()
    {
        var port = new FakeSerialPort(); using var client = NewSerial(port);
        await client.OpenAsync(); ISerialTransaction? escaped = null;
        port.Incoming.Enqueue([1, 2, 3]);
        await client.ExecuteTransactionAsync(async tx =>
        {
            escaped = tx;
            TestAssert.Bytes([1], await tx.ExchangeAsync([8], new FixedLengthFrame(1)));
            TestAssert.Bytes([2, 3], await tx.ReceiveAsync());
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([9]));
            return 0;
        });
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => escaped!.SendAsync([7]));
        port.Incoming.Enqueue([4, 5]);
        try { await client.ExchangeAsync([6], _ => ResponseDisposition.Reject); throw new Exception("응답 거부가 필요합니다."); }
        catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.ResponseRejected, error.Failure); }
        TestAssert.Equal(ClientState.Faulted, client.State);
    }

    private static SerialClient NewSerial(FakeSerialPort port) =>
        new(new SerialPortSettings("virtual"), new FixedLengthFrame(2), portFactory: _ => port);

    private sealed class FakeSerialPort : ISerialPortChannel
    {
        public readonly ConcurrentQueue<byte[]> Incoming = new();
        public readonly ConcurrentQueue<byte[]> Writes = new();
        public readonly TaskCompletionSource<bool> Written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public volatile bool Disposed;
        public void Open()
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(FakeSerialPort));
        }
        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(FakeSerialPort));
            if (!Incoming.TryDequeue(out var bytes)) { Thread.Sleep(Math.Min(5, timeoutMilliseconds)); throw new TimeoutException(); }
            if (bytes.Length > count)
                throw new InvalidOperationException("테스트 버퍼가 너무 작습니다.");
            bytes.CopyTo(buffer, offset); return bytes.Length;
        }
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            if (Disposed)
                throw new ObjectDisposedException(nameof(FakeSerialPort));
            Writes.Enqueue(TestBytes.Slice(buffer, offset, offset + count)); Written.TrySetResult(true);
        }
        public void DiscardInput() { while (Incoming.TryDequeue(out _)) { } }
        public void Dispose() => Disposed = true;
    }
}
