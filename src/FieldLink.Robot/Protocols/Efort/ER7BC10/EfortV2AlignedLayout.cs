namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT v2(현행 4바이트 정렬) 패킷 레이아웃 전략입니다.</summary>
internal sealed class EfortV2AlignedLayout : IEfortPacketLayout
{
    public int HeaderLength => 18;

    public int LengthFieldOffset => 16;

    public int PacketStartLength => 16;

    public int PacketEndLength => 16;

    public int RequestLength => 38;

    public int MinimumResponseLength => 788;

    public int PostDigitalInputOffset => 100;
}
