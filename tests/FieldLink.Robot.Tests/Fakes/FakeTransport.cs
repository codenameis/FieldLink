using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Tests.Fakes;

internal sealed class FakeTransport : ITransport
{
    private readonly Queue<byte[]> _incomingChunks = new();
    private readonly List<byte> _streamBuffer = new();

    public List<byte[]> SentFrames { get; } = new();

    public TransportState State { get; private set; } = TransportState.Closed;

    public long ConnectionId { get; private set; }

    public int OpenCount { get; private set; }

    public int CloseCount { get; private set; }

    public bool ThrowOnOpen { get; set; }

    public Func<byte[], byte[]?>? AutoResponder { get; set; }

    public Func<byte[], Exception?>? SendFault { get; set; }

    public Func<Exception?>? ReceiveFault { get; set; }

    public void EnqueueIncoming(byte[] bytes)
    {
        _incomingChunks.Enqueue((byte[])bytes.Clone());
    }

    public void EnqueueIncomingAscii(string ascii)
    {
        EnqueueIncoming(Encoding.ASCII.GetBytes(ascii));
    }

    public void EnqueueIncomingHex(string hex)
    {
        EnqueueIncoming(ParseHex(hex));
    }

    public void SimulateReconnect()
    {
        ConnectionId++;
        _streamBuffer.Clear();
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnOpen)
        {
            throw new TransportException(TransportFailure.ConnectFailed, "Simulated open failure");
        }

        OpenCount++;
        ConnectionId++;
        _streamBuffer.Clear();
        State = TransportState.Open;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        CloseCount++;
        _streamBuffer.Clear();
        State = TransportState.Closed;
        return Task.CompletedTask;
    }

    public Task<TResult> RunExclusiveAsync<TResult>(
        Func<ITransportSession, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        if (State != TransportState.Open)
        {
            throw new InvalidOperationException("FakeTransport is not open.");
        }

        var session = new FakeSession(this, ConnectionId, cancellationToken);
        return work(session);
    }

    public void Dispose()
    {
        _streamBuffer.Clear();
        State = TransportState.Disposed;
    }

    public static byte[] ParseHex(string hex)
    {
        string cleaned = hex.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        byte[] bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private sealed class FakeSession : ITransportSession
    {
        private readonly FakeTransport _owner;

        public FakeSession(FakeTransport owner, long connectionId, CancellationToken cancellationToken)
        {
            _owner = owner;
            ConnectionId = connectionId;
            CancellationToken = cancellationToken;
        }

        public long ConnectionId { get; }

        public CancellationToken CancellationToken { get; }

        public Task SendAsync(byte[] frame)
        {
            CancellationToken.ThrowIfCancellationRequested();
            _owner._streamBuffer.Clear();

            byte[] copy = (byte[])frame.Clone();
            _owner.SentFrames.Add(copy);

            Exception? fault = _owner.SendFault?.Invoke(copy);
            if (fault is not null)
            {
                throw fault;
            }

            byte[]? autoReply = _owner.AutoResponder?.Invoke(copy);
            if (autoReply is not null)
            {
                _owner._incomingChunks.Enqueue(autoReply);
            }

            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            CancellationToken.ThrowIfCancellationRequested();

            Exception? fault = _owner.ReceiveFault?.Invoke();
            if (fault is not null)
            {
                throw fault;
            }

            while (true)
            {
                if (_owner._streamBuffer.Count > 0)
                {
                    byte[] snapshot = _owner._streamBuffer.ToArray();
                    if (decoder.TryGetFrameLength(new ArraySegment<byte>(snapshot), out int frameLength))
                    {
                        byte[] frame = new byte[frameLength];
                        Array.Copy(snapshot, 0, frame, 0, frameLength);
                        _owner._streamBuffer.RemoveRange(0, frameLength);
                        return Task.FromResult(frame);
                    }
                }

                if (_owner._incomingChunks.Count == 0)
                {
                    throw new TransportException(TransportFailure.ConnectionClosed, "FakeTransport has no queued incoming data.");
                }

                byte[] chunk = _owner._incomingChunks.Dequeue();
                _owner._streamBuffer.AddRange(chunk);
            }
        }
    }
}
