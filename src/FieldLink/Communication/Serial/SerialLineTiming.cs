using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Serial
{
    // 포트 세대마다 하나를 만들고 포트 독점 구간에서만 사용한다. 제조사 클라이언트 간에도 간격을 공유한다.
    internal sealed class SerialLineTiming
    {
        private readonly SerialPortSettings settings;
        private readonly double bitsPerCharacter;
        private readonly Func<double> clock;
        private readonly Action<int, CancellationToken> wait;
        private double lastActivity, previousGap;

        internal SerialLineTiming(SerialPortSettings settings, Func<double> clock = null, Action<int, CancellationToken> wait = null)
        {
            this.settings = settings;
            bitsPerCharacter = 1 + settings.DataBits + (settings.Parity == Parity.None ? 0 : 1) +
                (settings.StopBits == StopBits.OnePointFive ? 1.5 : settings.StopBits == StopBits.Two ? 2 : 1);
            this.clock = clock ?? (() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
            this.wait = wait ?? ((milliseconds, token) => { token.WaitHandle.WaitOne(milliseconds); token.ThrowIfCancellationRequested(); });
            Reset();
        }

        internal void Reset() { lastActivity = clock(); previousGap = 0; }

        internal TimeSpan? GetMaximumReceiveInterval(IFrameBoundary boundary)
        {
            TimeSpan? interval = (boundary as ISerialReceiveTiming)?.GetMaximumInterCharacterInterval(settings.BaudRate, bitsPerCharacter);
            if (interval.HasValue && interval.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(boundary), "최대 문자 간격은 양수여야 합니다.");
            return interval;
        }

        internal void BeforeSend(IFrameBoundary boundary, CancellationToken token)
        {
            double gap = (boundary as ISerialFrameTiming)?.GetMinimumSilentInterval(settings.BaudRate, bitsPerCharacter).TotalMilliseconds ?? 0;
            if (double.IsNaN(gap) || gap < 0 || gap > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(boundary), "무통신 간격이 허용 범위를 벗어났습니다.");
            double required = Math.Max(previousGap, gap);
            double remaining;
            token.ThrowIfCancellationRequested();
            while (required > 0 && (remaining = lastActivity + required - clock()) > 0)
            {
                wait((int)Math.Min(int.MaxValue, Math.Ceiling(remaining)), token);
                token.ThrowIfCancellationRequested();
            }
            previousGap = gap;
        }

        // SerialPort.Write 반환이 물리 송신 완료와 같지는 않으므로 송신만 한 경우 문자 전송 시간도 보수적으로 포함한다.
        // 로컬 에코처럼 조기 수신된 바이트가 송신 종료 추정을 앞당기지 않도록 더 늦은 시각을 유지한다.
        internal void Sent(int bytes) => lastActivity = clock() + bytes * bitsPerCharacter * 1000.0 / settings.BaudRate;
        internal void Received() => lastActivity = Math.Max(lastActivity, clock());
    }
}
