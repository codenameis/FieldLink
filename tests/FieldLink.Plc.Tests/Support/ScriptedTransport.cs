using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Plc.Tests.Support;

/// <summary>미리 정해 둔 응답을 순서대로 돌려주는 시험용 전송입니다. 보낸 프레임도 기록합니다.</summary>
internal sealed class ScriptedTransport : ITransport
{
    private readonly ScriptedSession _session = new();

    /// <summary>프로토콜이 보낸 프레임 목록입니다.</summary>
    public IReadOnlyList<byte[]> SentFrames => _session.SentFrames;

    /// <inheritdoc />
    public TransportState State => TransportState.Open;

    /// <summary>다음 수신에서 돌려줄 응답을 고정 값으로 등록합니다.</summary>
    public void EnqueueResponse(byte[] response) => _session.Enqueue(_ => response);

    /// <summary>다음 수신에서 돌려줄 응답을 직전에 보낸 요청으로 계산해 등록합니다.</summary>
    public void EnqueueResponse(Func<byte[], byte[]> responder) => _session.Enqueue(responder);

    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<TResult> RunExclusiveAsync<TResult>(
        Func<ITransportSession, Task<TResult>> work,
        CancellationToken cancellationToken = default) => work(_session);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class ScriptedSession : ITransportSession
    {
        private readonly Queue<Func<byte[], byte[]>> _responses = new();
        private readonly List<byte[]> _sentFrames = new();

        public IReadOnlyList<byte[]> SentFrames => _sentFrames;

        public long ConnectionId => 1;

        public CancellationToken CancellationToken => CancellationToken.None;

        public void Enqueue(Func<byte[], byte[]> responder) => _responses.Enqueue(responder);

        public Task SendAsync(byte[] frame)
        {
            _sentFrames.Add(frame);
            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("등록된 응답이 없습니다.");
            }

            byte[] lastRequest = _sentFrames.Count > 0 ? _sentFrames[_sentFrames.Count - 1] : Array.Empty<byte>();
            return Task.FromResult(_responses.Dequeue()(lastRequest));
        }
    }
}
