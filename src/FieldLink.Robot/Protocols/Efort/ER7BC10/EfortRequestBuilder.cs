using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 바이너리 요청 패킷을 생성하는 순수 정적 빌더입니다.</summary>
public static class EfortRequestBuilder
{
    private const ushort ReadStatusOrder = 1001;
    private const int SequenceFieldOffset = 2;
    private const int TailFieldOffset = 4;
    private const string PacketHeadText = "MessageHead";
    private const string PacketTailText = "MessageTail";

    /// <summary>로봇 전체 상태 스냅샷 조회(<c>Order = 1001</c>) 요청 프레임을 생성합니다.</summary>
    /// <param name="sequence">16비트 순환 하트비트 시퀀스 번호입니다.</param>
    /// <param name="version">v2 정렬 또는 v1 비정렬 패킷 버전입니다.</param>
    /// <returns>직렬화된 요청 바이트 배열입니다.</returns>
    public static byte[] BuildReadCommand(ushort sequence, EfortProtocolVersion version = EfortProtocolVersion.V2Aligned)
    {
        IEfortPacketLayout layout = EfortOptions.CreateLayout(version);

        byte[] frame = new byte[layout.RequestLength];
        Encoding.ASCII.GetBytes(PacketHeadText, 0, PacketHeadText.Length, frame, 0);

        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, layout.LengthFieldOffset, (ushort)layout.RequestLength);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, layout.HeaderLength, ReadStatusOrder);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, layout.HeaderLength + SequenceFieldOffset, sequence);

        int tailOffset = layout.HeaderLength + TailFieldOffset;
        Encoding.ASCII.GetBytes(PacketTailText, 0, PacketTailText.Length, frame, tailOffset);
        return frame;
    }
}
