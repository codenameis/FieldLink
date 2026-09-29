using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Serial
{
    internal sealed class SerialTransaction : ISerialTransaction
    {
        private readonly ISerialPortChannel port;
        private readonly FrameAccumulator buffer;
        private readonly IFrameBoundary defaultBoundary;
        private readonly TimeSpan timeout;
        private readonly string portName;
        private readonly SerialLineTiming timing;
        private readonly Func<double> clock;
        private readonly double startedAt;
        private int active = 1, busy;
        private ExceptionDispatchInfo failure;
        internal CommunicationStage Stage { get; private set; } = CommunicationStage.Waiting;
        internal long BytesSent { get; private set; }
        internal long BytesReceived { get; private set; }
        public long ConnectionGeneration { get; }
        public CancellationToken CancellationToken { get; }
        internal SerialTransaction(ISerialPortChannel port, FrameAccumulator buffer, IFrameBoundary boundary,
            long generation, CancellationToken token, TimeSpan timeout, string portName, SerialLineTiming timing,
            Func<double> clock = null)
        {
            this.port = port; this.buffer = buffer; defaultBoundary = boundary; this.timeout = timeout;
            this.portName = portName; ConnectionGeneration = generation; CancellationToken = token;
            this.timing = timing;
            this.clock = clock ?? (() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
            startedAt = this.clock();
        }
        public Task SendAsync(byte[] message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return StepAsync(() => { Send(message, defaultBoundary); return true; });
        }
        public Task<byte[]> ReceiveAsync(IFrameBoundary boundary = null) => StepAsync(() => Receive(boundary ?? defaultBoundary));
        public Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary = null, Func<byte[], ResponseDisposition> classifyResponse = null)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return StepAsync(() =>
            {
                Send(request, boundary ?? defaultBoundary);
                while (true)
                {
                    byte[] response = Receive(boundary ?? defaultBoundary);
                    Stage = CommunicationStage.Validating;
                    ResponseDisposition disposition = classifyResponse?.Invoke(response) ?? ResponseDisposition.Accept;
                    Remaining();
                    if (disposition == ResponseDisposition.Accept)
                        return response;
                    if (disposition != ResponseDisposition.Ignore)
                        throw new CommunicationException(CommunicationFailure.ResponseRejected, Stage, portName,
                            BytesSent, BytesReceived, new InvalidDataException("응답 판정기가 프레임을 거부했습니다."));
                }
            });
        }
        public void DiscardInput()
        {
            Enter();
            try { CancellationToken.ThrowIfCancellationRequested(); port.DiscardInput(); buffer.Clear(); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); End(); throw; }
            finally { Volatile.Write(ref busy, 0); }
        }
        private void Send(byte[] message, IFrameBoundary responseBoundary)
        {
            CancellationToken.ThrowIfCancellationRequested(); Stage = CommunicationStage.Sending;
            if (message.Length == 0)
                return;
            // 이전 구현은 응답 길이가 충족되면 곧바로 다음 Write를 실행했다.
            // R-017: 간격을 요구하는 프레임은 포트별 마지막 활동 이후까지 기존 마감 시간 안에서 기다린다.
            timing.BeforeSend(responseBoundary, CancellationToken);
            port.Write(message, 0, message.Length, Remaining()); BytesSent += message.Length;
            timing.Sent(message.Length);
            CancellationToken.ThrowIfCancellationRequested();
        }
        private int Remaining()
        {
            CancellationToken.ThrowIfCancellationRequested();
            double remaining = timeout.TotalMilliseconds - (clock() - startedAt);
            // 이전: Math.Max(1, ...)로 만료된 요청도 1ms 읽기를 계속 허용했다.
            // 부분 수신과 응답 무시를 포함한 모든 단계가 생성 시점의 마감 시간을 공유한다.
            if (remaining <= 0)
                throw new TimeoutException("시리얼 요청의 전체 제한 시간이 지났습니다.");
            return Math.Max(1, (int)Math.Ceiling(remaining));
        }
        private byte[] Receive(IFrameBoundary boundary)
        {
            double lastReceivedAt = clock();
            byte[] overflowProbe = null;
            var timedPort = port as ITimedSerialPortChannel;
            TimeSpan? maximumInterval = timedPort == null ? null : timing.GetMaximumReceiveInterval(boundary);
            if (!maximumInterval.HasValue)
                timedPort = null;
            while (true)
            {
                int readTimeout = Math.Min(25, Remaining());
                Stage = CommunicationStage.Validating;
                byte[] complete = buffer.TryTake(boundary);
                if (complete != null)
                {
                    Remaining();
                    return complete;
                }
                bool waitingForSilence = boundary is IdleGapFrame gap && buffer.Count >= gap.MinimumLength;
                if (waitingForSilence)
                {
                    double idleRemaining = ((IdleGapFrame)boundary).Gap.TotalMilliseconds - (clock() - lastReceivedAt);
                    if (idleRemaining <= 0)
                        return buffer.Take(buffer.Count);
                    // 이전: port.Read(..., Math.Min(25, Remaining()))가 이미 지난 IdleGap을 넘겨 기다렸다.
                    // ReadTimeout의 정수 ms 해상도 안에서 남은 무수신 간격도 대기 상한으로 적용한다.
                    readTimeout = Math.Min(readTimeout, Math.Max(1, (int)Math.Ceiling(idleRemaining)));
                }
                // 이전: 가득 찬 버퍼에서도 FreeSpace()를 호출하여 정확히 최대 길이인 프레임을 거부했다.
                // 완료 경계가 남아 있으면 한 바이트만 확인한다. 침묵은 허용하고 실제 추가 수신은 거부한다.
                bool probingOverflow = waitingForSilence && buffer.IsFull;
                ArraySegment<byte> free = probingOverflow
                    ? new ArraySegment<byte>(overflowProbe ?? (overflowProbe = new byte[1]))
                    : buffer.FreeSpace();
                Stage = CommunicationStage.Receiving;
                int count;
                TimeSpan? silentInterval = null;
                readTimeout = Math.Min(readTimeout, Remaining());
                try
                {
                    // 이전: 길이와 CRC만 검사하고 프레임 내부의 물리적 수신 간격은 검사하지 않았다.
                    // 시간 정보를 제공하는 어댑터에서는 한 문자씩 소비하여 다음 프레임의 간격을 섞지 않는다.
                    // 기본 SerialPort.Read 반환 간격은 물리선의 간격이 아니므로 추정에 사용하지 않는다.
                    if (timedPort != null)
                    {
                        free.Array[free.Offset] = timedPort.ReadByteWithTiming(readTimeout, out silentInterval);
                        count = 1;
                    }
                    else
                        count = port.Read(free.Array, free.Offset, free.Count, readTimeout);
                }
                catch (TimeoutException)
                {
                    continue;
                } // 짧은 폴링 제한과 작업 전체의 마감 시간을 구분한다.

                BytesReceived += count;
                lastReceivedAt = clock();
                Remaining();
                if (count > 0)
                    timing.Received();
                if (silentInterval.HasValue && silentInterval.Value < TimeSpan.Zero)
                    throw new IOException("시리얼 어댑터가 음수 수신 간격을 반환했습니다.");
                if (buffer.Count > 0 && silentInterval.HasValue && silentInterval.Value > maximumInterval.Value)
                    throw new InvalidDataException("프레임 도중 허용된 문자 간 무수신 간격을 초과했습니다.");
                if (probingOverflow)
                {
                    if (count != 1)
                        throw new IOException("포트가 잘못된 수신 길이를 반환했습니다.");
                    throw new InvalidDataException("무수신 경계 전에 최대 프레임 길이를 초과했습니다.");
                }
                buffer.Advance(count);
            }
        }
        private async Task<T> StepAsync<T>(Func<T> action)
        {
            Enter();
            try { return await Task.Run(action).ConfigureAwait(false); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); End(); throw; }
            finally { Volatile.Write(ref busy, 0); }
        }
        private void Enter()
        {
            if (Volatile.Read(ref active) == 0)
                throw new InvalidOperationException("시리얼 트랜잭션이 종료되었습니다.");
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
                throw new InvalidOperationException("시리얼 교환은 순서대로 await해야 합니다.");
            if (Volatile.Read(ref active) == 0) { Volatile.Write(ref busy, 0); throw new InvalidOperationException("시리얼 트랜잭션이 종료되었습니다."); }
        }
        internal void End() => Interlocked.Exchange(ref active, 0);
        internal async Task<T> ExecuteAsync<T>(Func<ISerialTransaction, Task<T>> action)
        {
            try
            {
                CancellationToken.ThrowIfCancellationRequested();
                Task<T> work = action(this) ?? throw new InvalidOperationException("트랜잭션이 Task를 반환하지 않았습니다.");
                T result = await AwaitCancellationAsync(work, CancellationToken).ConfigureAwait(false);
                CancellationToken.ThrowIfCancellationRequested(); End(); failure?.Throw();
                if (Volatile.Read(ref busy) != 0)
                    throw new InvalidOperationException("진행 중인 시리얼 작업을 await하지 않고 콜백이 종료되었습니다.");
                return result;
            }
            finally { End(); }
        }
        internal static async Task<T> AwaitCancellationAsync<T>(Task<T> work, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false) != work)
                {
                    _ = work.ContinueWith(failed => { _ = failed.Exception; }, System.Threading.CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    token.ThrowIfCancellationRequested();
                }
                return await work.ConfigureAwait(false);
            }
        }
    }
}
