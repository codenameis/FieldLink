using System;
using System.IO.Ports;
using FieldLink.Plc;
using FieldLink.Transport;
using FieldLink.Transport.Serial;
using FieldLink.Transport.Tcp;

namespace FieldLink.PacketBench.Connections;

/// <summary>화면에서 읽은 연결 설정입니다. 생성 후 바뀌지 않습니다.</summary>
internal sealed class ConnectionSettings
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 502;

    public string SerialPortName { get; init; } = "COM1";

    public int BaudRate { get; init; } = 9600;

    public Parity Parity { get; init; } = Parity.Even;

    public int DataBits { get; init; } = 8;

    public StopBits StopBits { get; init; } = StopBits.One;

    /// <summary>국번·유닛 ID·CPU 번호로 쓰이는 값입니다.</summary>
    public byte Station { get; init; } = 1;

    public int TimeoutMilliseconds { get; init; } = 3000;

    public ByteOrder? ByteOrder { get; init; }

    public ITransport CreateTransport(TransportKind kind) => kind switch
    {
        TransportKind.Tcp => new TcpTransport(new TcpTransportOptions(Host, Port)),
        TransportKind.Serial => new SerialTransport(
            new SerialTransportOptions(SerialPortName, BaudRate, Parity, DataBits, StopBits)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 전송 종류입니다."),
    };

    public PlcClientOptions CreateClientOptions() => new()
    {
        Timeout = TimeSpan.FromMilliseconds(TimeoutMilliseconds),
        ByteOrder = ByteOrder,
    };
}
