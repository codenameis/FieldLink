using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;
using FieldLink.Transport.Udp;

namespace FieldLink.Robot.Tests.Fakes;

internal sealed class FakeUdpListenerTransport : IUdpListenerTransport
{
    private readonly Queue<UdpDatagram> _inboundQueue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _sync = new();

    public List<(byte[] Payload, IPEndPoint RemoteEndPoint)> SentDatagrams { get; } = new();

    public event Action<byte[], IPEndPoint>? DatagramSent;

    public TransportState State { get; private set; } = TransportState.Closed;

    public int BoundPort { get; set; } = 7127;

    public void EnqueueInbound(byte[] payload, IPEndPoint remoteEndPoint)
    {
        lock (_sync)
        {
            _inboundQueue.Enqueue(new UdpDatagram(payload, remoteEndPoint));
        }

        _signal.Release();
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = TransportState.Open;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        State = TransportState.Closed;
        return Task.CompletedTask;
    }

    public async Task<UdpDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (State != TransportState.Open)
        {
            throw new InvalidOperationException("Listener is not open.");
        }

        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            if (_inboundQueue.Count > 0)
            {
                return _inboundQueue.Dequeue();
            }
        }

        throw new TransportException(TransportFailure.ConnectionClosed, "Listener closed.");
    }

    public Task SendToAsync(byte[] payload, IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (State != TransportState.Open)
        {
            throw new InvalidOperationException("Listener is not open.");
        }

        byte[] copy = (byte[])payload.Clone();
        lock (_sync)
        {
            SentDatagrams.Add((copy, remoteEndPoint));
        }

        DatagramSent?.Invoke(copy, remoteEndPoint);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        State = TransportState.Disposed;
        _signal.Dispose();
    }
}
