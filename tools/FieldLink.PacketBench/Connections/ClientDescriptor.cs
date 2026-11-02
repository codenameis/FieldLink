using System;
using System.IO.Ports;
using FieldLink.Plc;
using FieldLink.Transport;

namespace FieldLink.PacketBench.Connections;

/// <summary>클라이언트 한 종류의 설명과 생성 방법입니다.</summary>
internal sealed class ClientDescriptor
{
    private readonly Func<ITransport, ConnectionSettings, PlcClient> _createClient;

    public ClientDescriptor(
        string id,
        string displayName,
        TransportKind transport,
        int defaultPort,
        bool usesStation,
        string addressExample,
        Func<ITransport, ConnectionSettings, PlcClient> createClient)
    {
        Id = id;
        DisplayName = displayName;
        Transport = transport;
        DefaultPort = defaultPort;
        UsesStation = usesStation;
        AddressExample = addressExample;
        _createClient = createClient ?? throw new ArgumentNullException(nameof(createClient));
    }

    public string Id { get; }

    public string DisplayName { get; }

    public TransportKind Transport { get; }

    public int DefaultPort { get; }

    /// <summary>국번(유닛 ID) 설정을 쓰는 클라이언트인지 여부입니다.</summary>
    public bool UsesStation { get; }

    /// <summary>이 프로토콜의 주소 표기 예입니다.</summary>
    public string AddressExample { get; }

    public int? DefaultBaudRate { get; init; }

    public Parity? DefaultParity { get; init; }

    public int? DefaultDataBits { get; init; }

    public PlcClient CreateClient(ITransport transport, ConnectionSettings settings) => _createClient(transport, settings);

    public override string ToString() => DisplayName;
}
