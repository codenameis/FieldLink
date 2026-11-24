using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai OnLTrack 64바이트 UDP 바이너리 패킷을 생성하는 순수 정적 빌더입니다.</summary>
public static class HyundaiPacketBuilder
{
    internal const int PacketLength = 64;
    internal const int AxisCount = 6;
    internal const int LinearAxisCount = 3;
    internal const int CharDummyOffset = 1;
    internal const int CharDummyMaxBytes = 3;
    internal const int StateOffset = 4;
    internal const int CountOffset = 8;
    internal const int IntDummyOffset = 12;
    internal const int DataOffset = 16;
    internal const int DoubleByteSize = sizeof(double);
    private const double MillimetersPerMeter = 1000.0;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const int StartAckState = 1;

    /// <summary><see cref="HyundaiTrackingPacket"/>을 64바이트 Little-Endian 와이어 패킷으로 인코딩합니다.</summary>
    /// <remarks>직선 축은 미터(m), 회전 축은 라디안(rad) 단위로 변환되며 <c>CharDummy</c>는 최대 3바이트로 절단됩니다.</remarks>
    public static byte[] BuildPacket(HyundaiTrackingPacket packet)
    {
        if (packet is null)
        {
            throw new ArgumentNullException(nameof(packet));
        }

        if (!Enum.IsDefined(typeof(HyundaiTrackingCommand), packet.Command))
        {
            throw new ArgumentOutOfRangeException(nameof(packet), packet.Command, "지원하지 않는 Hyundai OnLTrack 명령입니다.");
        }

        if (packet.Data.Count != AxisCount)
        {
            throw new ArgumentException($"트래킹 데이터 배열 길이는 {AxisCount}이어야 합니다.", nameof(packet));
        }

        byte[] frame = new byte[PacketLength];
        frame[0] = (byte)packet.Command;

        if (!string.IsNullOrEmpty(packet.CharDummy))
        {
            byte[] dummyBytes = Encoding.ASCII.GetBytes(packet.CharDummy);
            int copyLength = Math.Min(dummyBytes.Length, CharDummyMaxBytes);
            Array.Copy(dummyBytes, 0, frame, CharDummyOffset, copyLength);
        }

        RobotBinaryBuffer.WriteInt32LittleEndian(frame, StateOffset, packet.State);
        RobotBinaryBuffer.WriteInt32LittleEndian(frame, CountOffset, packet.Count);
        RobotBinaryBuffer.WriteInt32LittleEndian(frame, IntDummyOffset, packet.IntDummy);

        for (int i = 0; i < LinearAxisCount; i++)
        {
            RobotBinaryBuffer.WriteDoubleLittleEndian(frame, DataOffset + (i * DoubleByteSize), packet.Data[i] / MillimetersPerMeter);
        }

        for (int i = LinearAxisCount; i < AxisCount; i++)
        {
            RobotBinaryBuffer.WriteDoubleLittleEndian(frame, DataOffset + (i * DoubleByteSize), packet.Data[i] * DegreesToRadians);
        }

        return frame;
    }

    /// <summary>로봇의 <c>'S'</c> 시작 패킷 원본을 복사해 시작 확인 응답(<c>State=1, Count=0</c>)을 생성합니다.</summary>
    public static byte[] BuildStartAck(byte[] receivedRawPacket)
    {
        if (receivedRawPacket is null)
        {
            throw new ArgumentNullException(nameof(receivedRawPacket));
        }

        if (receivedRawPacket.Length != PacketLength)
        {
            throw new ArgumentException($"수신 패킷 길이는 {PacketLength}바이트여야 합니다.", nameof(receivedRawPacket));
        }

        byte[] ack = (byte[])receivedRawPacket.Clone();
        ack[0] = (byte)HyundaiTrackingCommand.Start;
        RobotBinaryBuffer.WriteInt32LittleEndian(ack, StateOffset, StartAckState);
        RobotBinaryBuffer.WriteInt32LittleEndian(ack, CountOffset, 0);
        return ack;
    }
}
