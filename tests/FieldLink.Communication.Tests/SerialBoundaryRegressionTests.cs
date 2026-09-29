using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.Communication.Serial;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task SerialIdleGapTenMillisecondsDoesNotMergeNextFrameAsync()
        => await AssertIdleGapBoundaryAsync(10, 15);

    internal static async Task SerialIdleGapFortyMillisecondsDoesNotMergeNextFrameAsync()
        => await AssertIdleGapBoundaryAsync(40, 45);

    private static async Task AssertIdleGapBoundaryAsync(int gap, int nextArrival)
    {
        using var port = new ScheduledSerialPort((0, new byte[] { 1 }), (nextArrival, new byte[] { 2 }));
        var transaction = CreateScheduledTransaction(port, new IdleGapFrame(TimeSpan.FromMilliseconds(gap)));
        TestAssert.Bytes(new byte[] { 1 }, await transaction.ReceiveAsync());
        TestAssert.Equal((double)gap, port.Now);
        TestAssert.Bytes(new byte[] { 2 }, await transaction.ReceiveAsync());
    }

    internal static async Task SerialIdleGapExactMaximumWaitsForSilenceAsync()
    {
        using var port = new ScheduledSerialPort((0, new byte[] { 1, 2, 3, 4 }));
        var transaction = CreateScheduledTransaction(port, new IdleGapFrame(TimeSpan.FromMilliseconds(40)), 4);
        TestAssert.Bytes(new byte[] { 1, 2, 3, 4 }, await transaction.ReceiveAsync());
        TestAssert.Equal(40.0, port.Now);
    }

    internal static async Task SerialIdleGapLengthMatrixAsync()
    {
        foreach (int initial in new[] { 3, 4, 5 })
        foreach (int? extraAt in new int?[] { null, 20, 45 })
        {
            byte[] first = Enumerable.Range(1, initial).Select(value => (byte)value).ToArray();
            var arrivals = new List<(double, byte[])> { (0, first) };
            if (extraAt.HasValue)
                arrivals.Add((extraAt.Value, new byte[] { 9 }));
            using var port = new ScheduledSerialPort(arrivals.ToArray());
            var transaction = CreateScheduledTransaction(port, new IdleGapFrame(TimeSpan.FromMilliseconds(40)), 4);
            int frameLength = initial + (extraAt == 20 ? 1 : 0);
            if (frameLength > 4)
                await TestAssert.ThrowsAsync<InvalidDataException>(() => transaction.ReceiveAsync());
            else
            {
                byte[] expected = extraAt == 20 ? first.Concat(new byte[] { 9 }).ToArray() : first;
                TestAssert.Bytes(expected, await transaction.ReceiveAsync());
                TestAssert.Equal(extraAt == 20 ? 60.0 : 40.0, port.Now);
                if (extraAt == 45)
                    TestAssert.Bytes(new byte[] { 9 }, await transaction.ReceiveAsync());
            }
        }
    }

    internal static async Task SerialFixedAndDelimitedMaximumBoundariesRemainUnchangedAsync()
    {
        foreach (IFrameBoundary boundary in new IFrameBoundary[] { new FixedLengthFrame(4), new DelimitedFrame(new byte[] { 4 }) })
        {
            using var port = new ScheduledSerialPort((0, new byte[] { 1, 2, 3, 4, 1, 2, 3, 4 }));
            var transaction = CreateScheduledTransaction(port, boundary, 4);
            TestAssert.Bytes(new byte[] { 1, 2, 3, 4 }, await transaction.ReceiveAsync());
            TestAssert.Bytes(new byte[] { 1, 2, 3, 4 }, await transaction.ReceiveAsync());
        }
        using var unterminated = new ScheduledSerialPort((0, new byte[] { 1, 2, 3, 5, 4 }));
        await TestAssert.ThrowsAsync<InvalidDataException>(() => CreateScheduledTransaction(unterminated,
            new DelimitedFrame(new byte[] { 4 }), 4).ReceiveAsync());
    }

    private static SerialTransaction CreateScheduledTransaction(ScheduledSerialPort port, IFrameBoundary boundary,
        int maximum = 64, int timeout = 500)
    {
        var timing = new SerialLineTiming(SerialPortSettings.Parse("COM1"), () => port.Now,
            (milliseconds, _) => port.Now += milliseconds);
        return new SerialTransaction(port, new FrameAccumulator(maximum), boundary, 1, CancellationToken.None,
            TimeSpan.FromMilliseconds(timeout), "virtual", timing, () => port.Now);
    }

    internal static async Task SerialPartialResponseKeepsOriginalDeadlineAsync()
    {
        using var port = new ScheduledSerialPort((0, new byte[] { 1 }), (30, new byte[] { 2 }), (60, new byte[] { 3 }));
        var transaction = CreateScheduledTransaction(port, new FixedLengthFrame(3), timeout: 50);
        await TestAssert.ThrowsAsync<TimeoutException>(() => transaction.ReceiveAsync());
        TestAssert.Equal(50.0, port.Now);
    }

    internal static async Task SerialIgnoredResponsesKeepOriginalDeadlineAsync()
    {
        using var port = new ScheduledSerialPort((0, new byte[] { 1 }), (30, new byte[] { 2 }), (60, new byte[] { 3 }));
        var transaction = CreateScheduledTransaction(port, new FixedLengthFrame(1), timeout: 50);
        await TestAssert.ThrowsAsync<TimeoutException>(() => transaction.ExchangeAsync(Array.Empty<byte>(),
            classifyResponse: _ => ResponseDisposition.Ignore));
        TestAssert.Equal(50.0, port.Now);
    }

    internal static async Task SerialClassifierCannotAcceptAfterDeadlineAsync()
    {
        using var port = new ScheduledSerialPort((0, new byte[] { 1 }));
        var transaction = CreateScheduledTransaction(port, new FixedLengthFrame(1), timeout: 50);
        await TestAssert.ThrowsAsync<TimeoutException>(() => transaction.ExchangeAsync(Array.Empty<byte>(),
            classifyResponse: _ => { port.Now = 51; return ResponseDisposition.Accept; }));
    }

    // Read 대기는 가상 시각만 진행한다. OS 스케줄링 지연을 물리선의 수신 간격으로 해석하지 않는다.
    private sealed class ScheduledSerialPort : ISerialPortChannel
    {
        private readonly Queue<(double At, byte[] Bytes)> arrivals;
        private int offset;
        internal double Now;
        internal ScheduledSerialPort(params (double, byte[])[] arrivals) => this.arrivals = new(arrivals);
        public void Open() { }
        public int Read(byte[] buffer, int start, int count, int timeoutMilliseconds)
        {
            if (Now > 2000)
                throw new IOException("가상 포트의 반복 제한을 초과했습니다.");
            if (arrivals.Count == 0 || arrivals.Peek().At > Now + timeoutMilliseconds)
            {
                Now += timeoutMilliseconds;
                throw new TimeoutException();
            }
            var next = arrivals.Peek();
            Now = Math.Max(Now, next.At);
            int copied = Math.Min(count, next.Bytes.Length - offset);
            Array.Copy(next.Bytes, offset, buffer, start, copied);
            offset += copied;
            if (offset == next.Bytes.Length)
            {
                arrivals.Dequeue();
                offset = 0;
            }
            return copied;
        }
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds) { }
        public void DiscardInput() { arrivals.Clear(); offset = 0; }
        public void Dispose() { }
    }
}
