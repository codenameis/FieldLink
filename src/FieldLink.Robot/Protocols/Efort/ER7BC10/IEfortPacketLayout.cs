namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT 패킷 버전별(v1 비정렬 / v2 정렬) 오프셋 및 길이 전략입니다.</summary>
internal interface IEfortPacketLayout
{
    int HeaderLength { get; }

    int LengthFieldOffset { get; }

    int PacketStartLength { get; }

    int PacketEndLength { get; }

    int RequestLength { get; }

    int MinimumResponseLength { get; }

    int PostDigitalInputOffset { get; }
}
