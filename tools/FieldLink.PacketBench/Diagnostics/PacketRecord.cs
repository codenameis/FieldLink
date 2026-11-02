using System;

namespace FieldLink.PacketBench.Diagnostics;

/// <summary>패킷 로그 한 줄입니다. 생성 후 바뀌지 않습니다.</summary>
internal sealed class PacketRecord
{
    public PacketRecord(
        long sequence,
        DateTime timestamp,
        PacketSource source,
        PacketDirection direction,
        byte[] frame,
        double? elapsedMilliseconds,
        string note)
    {
        Sequence = sequence;
        Timestamp = timestamp;
        Source = source;
        Direction = direction;
        Frame = frame;
        ElapsedMilliseconds = elapsedMilliseconds;
        Note = note;
    }

    public long Sequence { get; }

    public DateTime Timestamp { get; }

    public PacketSource Source { get; }

    public PacketDirection Direction { get; }

    public byte[] Frame { get; }

    /// <summary>직전 송신 이후 이 프레임을 받기까지 걸린 시간입니다. 송신 프레임이면 null입니다.</summary>
    public double? ElapsedMilliseconds { get; }

    public string Note { get; }

    public int Length => Frame.Length;

    public string Hex => HexText.Format(Frame);

    public string DirectionText => Direction == PacketDirection.Sent ? "TX ▶" : "◀ RX";

    public string SourceText => Source == PacketSource.Client ? "클라이언트" : "가상 PLC";

    public string ElapsedText => ElapsedMilliseconds is null ? string.Empty : $"{ElapsedMilliseconds.Value:F1} ms";
}
