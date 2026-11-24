namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT v1(구버전 비정렬) 패킷 레이아웃 전략입니다.</summary>
internal sealed class EfortV1UnalignedLayout : IEfortPacketLayout
{
    public int HeaderLength => 17;

    public int LengthFieldOffset => 15;

    public int PacketStartLength => 15;

    public int PacketEndLength => 15;

    public int RequestLength => 36;

    public int MinimumResponseLength => 784;

    public int PostDigitalInputOffset => 97;
}
