using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.PacketBench.Diagnostics;

/// <summary>실제 전송을 감싸 보내고 받은 프레임을 그대로 기록합니다.</summary>
/// <remarks>프로토콜이 만든 바이트를 그대로 보므로 패킷 단위 검증에 쓸 수 있습니다.</remarks>
internal sealed class RecordingTransport : ITransport
{
    private readonly ITransport _inner;
    private readonly PacketLog _log;

    public RecordingTransport(ITransport inner, PacketLog log)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public TransportState State => _inner.State;

    public Task OpenAsync(CancellationToken cancellationToken = default) => _inner.OpenAsync(cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken = default) => _inner.CloseAsync(cancellationToken);

    public Task<TResult> RunExclusiveAsync<TResult>(
        Func<ITransportSession, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        if (work is null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        return _inner.RunExclusiveAsync(
            session => work(new RecordingSession(session, _log)),
            cancellationToken);
    }

    public void Dispose() => _inner.Dispose();

    private sealed class RecordingSession : ITransportSession
    {
        private readonly ITransportSession _inner;
        private readonly PacketLog _log;
        private readonly Stopwatch _stopwatch = new();

        public RecordingSession(ITransportSession inner, PacketLog log)
        {
            _inner = inner;
            _log = log;
        }

        public long ConnectionId => _inner.ConnectionId;

        public CancellationToken CancellationToken => _inner.CancellationToken;

        public async Task SendAsync(byte[] frame)
        {
            _log.Record(PacketSource.Client, PacketDirection.Sent, frame);
            _stopwatch.Restart();
            await _inner.SendAsync(frame).ConfigureAwait(false);
        }

        public async Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            byte[] frame = await _inner.ReceiveAsync(decoder).ConfigureAwait(false);
            _log.Record(
                PacketSource.Client,
                PacketDirection.Received,
                frame,
                _stopwatch.IsRunning ? _stopwatch.Elapsed.TotalMilliseconds : null);
            return frame;
        }
    }
}
