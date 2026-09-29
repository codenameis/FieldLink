using System.Collections.Concurrent;
using System.Diagnostics;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task SerialLateOpenCannotReplaceNewSessionAsync()
    {
        var first = new ControlledSerialPort { BlockOpen = true };
        var second = new ControlledSerialPort();
        var ports = new Queue<ControlledSerialPort>([first, second]);
        using var client = NewRaceClient(ports);
        try
        {
            Task opening = client.OpenAsync(TimeSpan.FromMilliseconds(80));
            await first.OpenStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            try { await opening.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("열기 시간 초과가 필요합니다."); }
            catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.Timeout, error.Failure); }
            TestAssert.True(first.Disposed);
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());

            first.AllowOpen.Set();
            await first.OpenFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await OpenAfterLateWorkerAsync(client);
            TestAssert.Equal(ClientState.Open, client.State);
            TestAssert.True(!second.Disposed);
            await client.SendAsync([0x42]);
            TestAssert.Equal(1, second.Writes.Count);
            TestAssert.Equal(0, first.Writes.Count);
        }
        finally { first.AllowOpen.Set(); }
    }

    internal static async Task SerialOpenWaitsForPriorCloseReleaseAsync()
    {
        var first = new ControlledSerialPort { BlockDispose = true };
        var second = new ControlledSerialPort();
        var ports = new Queue<ControlledSerialPort>([first, second]);
        using var client = NewRaceClient(ports);
        await client.OpenAsync();
        try
        {
            Task closing = Task.Run(client.Close);
            await first.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());
            TestAssert.Equal(0, second.OpenCount);
            first.AllowDispose.Set();
            await closing.WaitAsync(TimeSpan.FromSeconds(2));
            await client.OpenAsync();
            TestAssert.Equal(ClientState.Open, client.State);
            TestAssert.Equal(1, second.OpenCount);
        }
        finally { first.AllowDispose.Set(); }
    }

    internal static async Task SerialEscapedTransactionCannotUseReopenedPortAsync()
    {
        var first = new ControlledSerialPort();
        var second = new ControlledSerialPort();
        var ports = new Queue<ControlledSerialPort>([first, second]);
        using var client = NewRaceClient(ports);
        await client.OpenAsync();
        ISerialTransaction? escaped = null;
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> transaction = client.ExecuteTransactionAsync(tx =>
        {
            escaped = tx;
            return pending.Task; // 사용자 콜백이 취소를 무시한다.
        }, TimeSpan.FromMilliseconds(80));
        try
        {
            try { await transaction.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("트랜잭션 시간 초과가 필요합니다."); }
            catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.Timeout, error.Failure); }
            TestAssert.True(first.Disposed);
            await client.OpenAsync();
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => escaped!.SendAsync([0x55]));
            TestAssert.Equal(0, second.Writes.Count);
            await client.SendAsync([0x66]);
            TestAssert.Equal(1, second.Writes.Count);
        }
        finally { pending.TrySetResult(1); }
    }

    internal static async Task SerialDetachedWriteCannotReachReopenedPortAsync()
    {
        var first = new ControlledSerialPort { BlockWrite = true };
        var second = new ControlledSerialPort();
        var ports = new Queue<ControlledSerialPort>([first, second]);
        using var client = NewRaceClient(ports);
        await client.OpenAsync();
        try
        {
            Task writing = client.SendAsync([0x11], TimeSpan.FromMilliseconds(80));
            await first.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            try { await writing.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("쓰기 시간 초과가 필요합니다."); }
            catch (CommunicationException error) { TestAssert.Equal(CommunicationFailure.Timeout, error.Failure); }
            await client.OpenAsync();
            first.AllowWrite.Set();
            await first.WriteFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
            TestAssert.Equal(0, second.Writes.Count);
            await client.SendAsync([0x22]);
            TestAssert.Equal(1, second.Writes.Count);
        }
        finally { first.AllowWrite.Set(); }
    }

    private static SerialClient NewRaceClient(Queue<ControlledSerialPort> ports) =>
        new(new SerialPortSettings("virtual"), new FixedLengthFrame(1), portFactory: _ => ports.Dequeue());

    private static async Task OpenAfterLateWorkerAsync(SerialClient client)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try { await client.OpenAsync(); return; }
            catch (InvalidOperationException) when (elapsed.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Port.Open 완료 신호와 SerialClient의 작업자 정리는 연속된 별도 단계다.
                await Task.Delay(5);
            }
        }
    }

    private sealed class ControlledSerialPort : ISerialPortChannel
    {
        internal readonly TaskCompletionSource<bool> OpenStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> OpenFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> DisposeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> WriteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> WriteFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim AllowOpen = new(false);
        internal readonly ManualResetEventSlim AllowDispose = new(false);
        internal readonly ManualResetEventSlim AllowWrite = new(false);
        internal readonly ConcurrentQueue<byte[]> Writes = new();
        internal bool BlockOpen, BlockDispose, BlockWrite;
        internal volatile bool Disposed;
        internal int OpenCount;

        public void Open()
        {
            OpenStarted.TrySetResult(true);
            try
            {
                if (BlockOpen)
                    AllowOpen.Wait(); // 의도적으로 취소·Dispose를 무시하는 드라이버.
                Interlocked.Increment(ref OpenCount);
            }
            finally { OpenFinished.TrySetResult(true); }
        }
        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds) => throw new TimeoutException();
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            WriteStarted.TrySetResult(true);
            try
            {
                if (BlockWrite)
                    AllowWrite.Wait(); // 이미 진입한 I/O가 늦게 끝나는 상황.
                Writes.Enqueue(TestBytes.Slice(buffer, offset, offset + count));
            }
            finally { WriteFinished.TrySetResult(true); }
        }
        public void DiscardInput() { }
        public void Dispose()
        {
            DisposeStarted.TrySetResult(true);
            if (BlockDispose)
                AllowDispose.Wait();
            Disposed = true;
        }
    }
}
