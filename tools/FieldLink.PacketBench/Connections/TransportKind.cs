namespace FieldLink.PacketBench.Connections;

/// <summary>클라이언트가 사용하는 전송 종류입니다.</summary>
internal enum TransportKind
{
    /// <summary>TCP 소켓입니다.</summary>
    Tcp,

    /// <summary>시리얼 포트입니다.</summary>
    Serial,
}
