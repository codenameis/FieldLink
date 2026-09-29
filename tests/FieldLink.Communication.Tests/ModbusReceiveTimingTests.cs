using FieldLink.PlcDrivers.Modbus;
using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task ModbusRtuRejectsCrcValidResponseWithExcessiveCharacterGapAsync()
    {
        // 공식 §2.5.1.1: 8E1의 11비트, 19200 초과는 권고 고정값 750us.
        foreach (var setting in new[] { (Baud: 9600, LimitTicks: 17188L), (Baud: 19200, LimitTicks: 8594L), (Baud: 38400, LimitTicks: 7500L) })
        {
            var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B533"), 3,
                TimeSpan.FromTicks(setting.LimitTicks + 1));
            using var transport = TimedModbusTransport(port, setting.Baud);
            await transport.OpenAsync();
            var client = new ModbusSerialClient(transport);
            var error = await TestAssert.FailureAsync(() => client.ReadAsync("0", 1), CommunicationFailure.InvalidFrame);
            TestAssert.Equal(4L, error.BytesReceived);
            TestAssert.True(port.Disposed);
            TestAssert.Equal(1, port.Writes);
        }
    }

    internal static async Task ModbusRtuAcceptsCharacterGapsAtOrBelowLimitAsync()
    {
        foreach (long ticks in new[] { 0L, 7499L, 7500L })
        {
            var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B533"), 3, TimeSpan.FromTicks(ticks));
            using var transport = TimedModbusTransport(port, 38400);
            await transport.OpenAsync();
            var result = await new ModbusSerialClient(transport).ReadAsync("0", 1);
            TestAssert.True(result.IsSuccess, result.Message);
            TestAssert.Bytes(new byte[] { 0x12, 0x34 }, result.Content);
            TestAssert.Equal(7, port.TimedReads);
        }
    }

    internal static async Task ModbusRtuDoesNotApplyGapBetweenCompleteFramesAsync()
    {
        var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B5330103021234B533"), 7, TimeSpan.FromSeconds(1));
        using var transport = TimedModbusTransport(port, 38400);
        await transport.OpenAsync();
        var client = new ModbusSerialClient(transport);
        TestAssert.True((await client.ReadAsync("0", 1)).IsSuccess);
        TestAssert.True((await client.ReadAsync("0", 1)).IsSuccess);
        TestAssert.Equal(14, port.TimedReads);
    }

    internal static async Task ModbusRtuUnknownTimingPreservesCrcValidationAsync()
    {
        foreach (bool validCrc in new[] { true, false })
        {
            var port = new TimedResponsePort(TestBytes.FromHexString(validCrc ? "0103021234B533" : "0103021234B532"), 3, null);
            using var transport = TimedModbusTransport(port, 38400);
            await transport.OpenAsync();
            var result = await new ModbusSerialClient(transport).ReadAsync("0", 1);
            TestAssert.Equal(validCrc, result.IsSuccess);
        }
    }

    internal static async Task ModbusAsciiDoesNotUseRtuReceiveTimingAsync()
    {
        var port = new TimedResponsePort(System.Text.Encoding.ASCII.GetBytes(":0103021234B4\r\n"), 3, TimeSpan.FromSeconds(1));
        using var transport = TimedModbusTransport(port, 9600);
        await transport.OpenAsync();
        TestAssert.True((await new ModbusSerialClient(transport, ModbusSerialEncoding.Ascii).ReadAsync("0", 1)).IsSuccess);
        TestAssert.Equal(0, port.TimedReads);
    }

    internal static async Task SerialTimedAdapterRejectsNegativeGapMetadataAsync()
    {
        var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B533"), 3, TimeSpan.FromTicks(-1));
        using var transport = TimedModbusTransport(port, 38400);
        await transport.OpenAsync();
        await TestAssert.ThrowsAsync<CommunicationException>(() => new ModbusSerialClient(transport).ReadAsync("0", 1));
        TestAssert.True(port.Disposed);
    }

    internal static async Task ModbusRtuPreviouslyBufferedBytesHaveUnknownTimingAsync()
    {
        var port = new TimedResponsePort(TestBytes.FromHexString("990103021234B533"), 4, TimeSpan.FromSeconds(1))
        { BulkChunkSize = 8 };
        using var transport = TimedModbusTransport(port, 38400);
        await transport.OpenAsync();
        TestAssert.Bytes(new byte[] { 0x99 }, await transport.ExchangeAsync(new byte[] { 0 }));
        TestAssert.True((await new ModbusSerialClient(transport).ReadAsync("0", 1)).IsSuccess);
        TestAssert.Equal(0, port.TimedReads); // 이전 일반 Read가 남긴 바이트의 간격은 복원할 수 없다.
    }

    internal static async Task SerialUntimedReadReturnGapIsNotPhysicalTimingAsync()
    {
        byte[] response = TestBytes.FromHexString("0103021234B533");
        using var port = new ScheduledSerialPort((0, TestBytes.Slice(response, 0, 3)), (15, TestBytes.Slice(response, 3)));
        var transaction = CreateScheduledTransaction(port, new ReceiveGapTestBoundary());
        TestAssert.Bytes(response, await transaction.ReceiveAsync());
        TestAssert.Equal(15.0, port.Now); // Read 반환이 15ms 벌어져도 물리 문자 간격을 증명하지 않는다.
    }

    internal static async Task SerialTimedReadReturnDelayDoesNotOverridePhysicalMetadataAsync()
    {
        var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B533"), 3, TimeSpan.Zero)
        { ReadElapsedMilliseconds = 10 };
        var transaction = CreateTimedReceiveTransaction(port, 500);
        TestAssert.Bytes(TestBytes.FromHexString("0103021234B533"), await transaction.ReceiveAsync());
        TestAssert.Equal(70.0, port.Now); // 신뢰 가능한 물리 간격 0과 호스트 실행 지연을 구분한다.
    }

    internal static async Task SerialTimedReceiveStillUsesWholeRequestDeadlineAsync()
    {
        var port = new TimedResponsePort(TestBytes.FromHexString("0103021234B533"), 3, TimeSpan.Zero)
        { ReadElapsedMilliseconds = 10 };
        var transaction = CreateTimedReceiveTransaction(port, 50);
        await TestAssert.ThrowsAsync<TimeoutException>(() => transaction.ReceiveAsync());
        TestAssert.Equal(50.0, port.Now);
        TestAssert.Equal(5L, transaction.BytesReceived);
    }

    private static SerialTransaction CreateTimedReceiveTransaction(TimedResponsePort port, int timeout)
    {
        var timing = new SerialLineTiming(new SerialPortSettings("virtual", baudRate: 38400), () => port.Now,
            (milliseconds, _) => port.Now += milliseconds);
        return new SerialTransaction(port, new FrameAccumulator(64), new ReceiveGapTestBoundary(), 1,
            CancellationToken.None, TimeSpan.FromMilliseconds(timeout), "virtual", timing, () => port.Now);
    }

    private sealed class ReceiveGapTestBoundary : ISerialReceiveTiming
    {
        public TimeSpan GetMaximumInterCharacterInterval(int baudRate, double bitsPerCharacter) => TimeSpan.FromTicks(7500);
        public int? GetFrameLength(ArraySegment<byte> bytes) => 7;
    }

    private static SerialClient TimedModbusTransport(TimedResponsePort port, int baud) => new(
        SerialPortSettings.Parse($"COM1-{baud}-8-E-1"), new FixedLengthFrame(1), portFactory: _ => port);

    // 완성된 CRC 바이트열과 수신 시각 정보를 독립적으로 공급한다. Read 청크 경계로 간격을 추정하지 않는다.
    private sealed class TimedResponsePort : ITimedSerialPortChannel
    {
        private readonly byte[] bytes;
        private readonly int gapIndex;
        private readonly TimeSpan? gap;
        private int index;
        internal int TimedReads, Writes;
        internal int BulkChunkSize = 2, ReadElapsedMilliseconds;
        internal double Now;
        internal bool Disposed;
        internal TimedResponsePort(byte[] bytes, int gapIndex, TimeSpan? gap)
        { this.bytes = bytes; this.gapIndex = gapIndex; this.gap = gap; }
        public void Open() { }
        public byte ReadByteWithTiming(int timeoutMilliseconds, out TimeSpan? minimumSilentInterval)
        {
            if (index == bytes.Length)
                throw new TimeoutException();
            minimumSilentInterval = index == 0 ? TimeSpan.FromSeconds(1) : index == gapIndex ? gap : TimeSpan.Zero;
            TimedReads++;
            Now += ReadElapsedMilliseconds;
            return bytes[index++];
        }
        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            if (index == bytes.Length)
                throw new TimeoutException();
            int copied = Math.Min(BulkChunkSize, Math.Min(count, bytes.Length - index));
            Array.Copy(bytes, index, buffer, offset, copied);
            index += copied;
            return copied;
        }
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds) => Writes++;
        public void DiscardInput() => index = 0;
        public void Dispose() => Disposed = true;
    }
}
