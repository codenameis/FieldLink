using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace FieldLink.PacketBench.Diagnostics;

/// <summary>여러 스레드에서 쌓이고 UI 타이머가 비워 가는 패킷 기록 대기열입니다.</summary>
internal sealed class PacketLog
{
    private const int PendingLimit = 4000;

    private readonly ConcurrentQueue<PacketRecord> _pending = new();

    private long _sequence;
    private long _sentCount;
    private long _receivedCount;

    /// <summary>가상 PLC 서버가 주고받은 프레임도 기록할지 여부입니다. 기본값은 false입니다.</summary>
    public bool RecordServerFrames { get; set; }

    public long SentCount => Interlocked.Read(ref _sentCount);

    public long ReceivedCount => Interlocked.Read(ref _receivedCount);

    public int PendingCount => _pending.Count;

    public void Record(
        PacketSource source,
        PacketDirection direction,
        byte[] frame,
        double? elapsedMilliseconds = null,
        string note = "")
    {
        if (source == PacketSource.VirtualPlc && !RecordServerFrames)
        {
            return;
        }

        if (direction == PacketDirection.Sent)
        {
            Interlocked.Increment(ref _sentCount);
        }
        else
        {
            Interlocked.Increment(ref _receivedCount);
        }

        var record = new PacketRecord(
            Interlocked.Increment(ref _sequence),
            DateTime.Now,
            source,
            direction,
            (byte[])frame.Clone(),
            elapsedMilliseconds,
            note);

        _pending.Enqueue(record);

        while (_pending.Count > PendingLimit && _pending.TryDequeue(out _))
        {
        }
    }

    public bool TryDequeue([NotNullWhen(true)] out PacketRecord? record) => _pending.TryDequeue(out record);

    public void Reset()
    {
        while (_pending.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _sequence, 0);
        Interlocked.Exchange(ref _sentCount, 0);
        Interlocked.Exchange(ref _receivedCount, 0);
    }
}
