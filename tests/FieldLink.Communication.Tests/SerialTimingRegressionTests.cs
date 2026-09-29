using FieldLink.PlcDrivers.Modbus.Clients;
using System.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task ReviewSerialSilenceUsesVirtualClockAndCancellationAsync()
    {
        double now = 0;
        var waits = new List<int>();
        var timing = new SerialLineTiming(new SerialPortSettings("virtual"), () => now, (milliseconds, token) =>
        {
            token.ThrowIfCancellationRequested();
            waits.Add(milliseconds);
            now += milliseconds;
        });
        var frame = new TestTimedFrame();
        timing.BeforeSend(frame, CancellationToken.None);
        TestAssert.Equal(2, waits.Single()); // 1.750ms를 1ms로 내리지 않는다.
        now = 100;
        timing.Received();
        timing.BeforeSend(new FixedLengthFrame(1), CancellationToken.None);
        TestAssert.Equal(102d, now); // 다음 요청이 다른 프로토콜이어도 직전 프레임 간격은 지킨다.
        timing.Sent(960); // 9600 baud의 960문자는 1000ms. Write가 아직 물리 송신 중일 수 있다.
        now += 10;
        timing.Received(); // 조기 수신/로컬 에코가 송신 종료 추정을 앞당기면 안 된다.
        timing.BeforeSend(frame, CancellationToken.None);
        TestAssert.True(now >= 1103.75, "조기 수신이 예상 송신 종료 시간을 덮었습니다.");
        timing.Reset();
        int before = waits.Count;
        timing.BeforeSend(new FixedLengthFrame(1), CancellationToken.None);
        TestAssert.Equal(before, waits.Count); // 새 세대에는 이전 규칙을 남기지 않는다.
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() =>
        {
            timing.BeforeSend(frame, cancelled.Token);
            return Task.CompletedTask;
        });
        TestAssert.Equal(before, waits.Count);
    }

    internal static async Task ReviewRtuGapHonorsDeadlineBeforeSendAndReopenAsync()
    {
        var first = new FakeSerialPort();
        var second = new FakeSerialPort();
        var ports = new Queue<FakeSerialPort>([first, second]);
        using var transport = new SerialClient(new SerialPortSettings("virtual", baudRate: 1), new FixedLengthFrame(1),
            portFactory: _ => ports.Dequeue());
        await transport.OpenAsync();
        var device = new ModbusSerialClient(transport, timeout: TimeSpan.FromMilliseconds(60));
        await TestAssert.FailureAsync(() => device.ReadAsync("0", 1), FieldLink.Communication.Diagnostics.CommunicationFailure.Timeout);
        TestAssert.Equal(0, first.Writes.Count);
        TestAssert.True(first.Disposed);
        await transport.OpenAsync();
        second.Incoming.Enqueue([7]);
        TestAssert.Bytes([7], await transport.ExchangeAsync([1], TimeSpan.FromSeconds(1)));
        TestAssert.Equal(1, second.Writes.Count);
    }

    internal static async Task ReviewRtuGapAcrossSegmentsAndClientsAsync()
    {
        var port = new TimedSerialPort();
        // 100 baud, 8-N-1이면 t3.5=350ms. 느린 가상 회선으로 스케줄러 오차와 구분한다.
        using var transport = new SerialClient(new SerialPortSettings("virtual", baudRate: 100), new FixedLengthFrame(1),
            timeout: TimeSpan.FromSeconds(5), portFactory: _ => port);
        await transport.OpenAsync();
        var first = new ModbusSerialClient(transport, timeout: TimeSpan.FromSeconds(5));
        var second = new ModbusSerialClient(transport, timeout: TimeSpan.FromSeconds(5));
        byte[] large = new byte[243];
        large[0] = 1; large[1] = 3; large[2] = 240;
        port.Inner.Incoming.Enqueue(Crc16.Append(large));
        port.Inner.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("0103021234")));
        port.Inner.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("0103025678")));
        TestAssert.True((await first.ReadAsync("0", 121)).IsSuccess);
        TestAssert.True((await second.ReadAsync("0", 1)).IsSuccess);
        TestAssert.Equal(3, port.SendTimes.Count);
        for (int i = 1; i < port.SendTimes.Count; i++)
            TestAssert.True(port.SendTimes[i] - port.ReceiveTimes[i - 1] >= 350,
                "이전 수신 이후 t3.5 전에 다음 구간 또는 다른 클라이언트가 송신했습니다.");
    }

    private sealed class TimedSerialPort : ISerialPortChannel
    {
        internal readonly FakeSerialPort Inner = new();
        internal readonly List<double> SendTimes = new(), ReceiveTimes = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public void Open() => Inner.Open();
        public int Read(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            int received = Inner.Read(buffer, offset, count, timeoutMilliseconds);
            ReceiveTimes.Add(clock.Elapsed.TotalMilliseconds);
            return received;
        }
        public void Write(byte[] buffer, int offset, int count, int timeoutMilliseconds)
        {
            SendTimes.Add(clock.Elapsed.TotalMilliseconds);
            Inner.Write(buffer, offset, count, timeoutMilliseconds);
        }
        public void DiscardInput() => Inner.DiscardInput();
        public void Dispose() => Inner.Dispose();
    }

    private sealed class TestTimedFrame : FieldLink.Communication.ISerialFrameTiming
    {
        public TimeSpan GetMinimumSilentInterval(int baudRate, double bitsPerCharacter) => TimeSpan.FromTicks(17500);
        public int? GetFrameLength(ArraySegment<byte> bytes) => 1;
    }
}
