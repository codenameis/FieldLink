using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai OnLTrack 64바이트 UDP 바이너리 패킷을 해석하는 순수 정적 파서입니다.</summary>
public static class HyundaiPacketParser
{
    private const double MillimetersPerMeter = 1000.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    /// <summary>수신된 64바이트 데이터그램을 <see cref="HyundaiTrackingPacket"/>으로 디코딩합니다.</summary>
    public static HyundaiTrackingPacket ParsePacket(byte[] datagram)
    {
        if (datagram is null)
        {
            throw new ArgumentNullException(nameof(datagram));
        }

        if (datagram.Length != HyundaiPacketBuilder.PacketLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Hyundai OnLTrack 패킷 길이가 {HyundaiPacketBuilder.PacketLength}바이트가 아닙니다. (실제: {datagram.Length}바이트)",
                datagram);
        }

        byte commandByte = datagram[0];
        if (!Enum.IsDefined(typeof(HyundaiTrackingCommand), commandByte))
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"알 수 없는 Hyundai OnLTrack 명령 바이트입니다. (0x{commandByte:X2})",
                datagram);
        }

        double[] data = new double[HyundaiPacketBuilder.AxisCount];
        for (int i = 0; i < HyundaiPacketBuilder.LinearAxisCount; i++)
        {
            double value = RobotBinaryBuffer.ReadDoubleLittleEndian(
                datagram,
                HyundaiPacketBuilder.DataOffset + (i * HyundaiPacketBuilder.DoubleByteSize)) * MillimetersPerMeter;
            ValidateFiniteWireDouble(value, i, datagram);
            data[i] = value;
        }

        for (int i = HyundaiPacketBuilder.LinearAxisCount; i < HyundaiPacketBuilder.AxisCount; i++)
        {
            double value = RobotBinaryBuffer.ReadDoubleLittleEndian(
                datagram,
                HyundaiPacketBuilder.DataOffset + (i * HyundaiPacketBuilder.DoubleByteSize)) * RadiansToDegrees;
            ValidateFiniteWireDouble(value, i, datagram);
            data[i] = value;
        }

        return new HyundaiTrackingPacket
        {
            Command = (HyundaiTrackingCommand)commandByte,
            CharDummy = RobotBinaryBuffer.ReadNullTrimmedString(
                datagram,
                HyundaiPacketBuilder.CharDummyOffset,
                HyundaiPacketBuilder.CharDummyMaxBytes,
                Encoding.ASCII),
            State = RobotBinaryBuffer.ReadInt32LittleEndian(datagram, HyundaiPacketBuilder.StateOffset),
            Count = RobotBinaryBuffer.ReadInt32LittleEndian(datagram, HyundaiPacketBuilder.CountOffset),
            IntDummy = RobotBinaryBuffer.ReadInt32LittleEndian(datagram, HyundaiPacketBuilder.IntDummyOffset),
            Data = data
        };
    }

    private static void ValidateFiniteWireDouble(double value, int axisIndex, byte[] datagram)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Hyundai OnLTrack 패킷의 축 인덱스 {axisIndex}에 유한하지 않은 실수 값이 포함되어 있습니다.",
                datagram);
        }
    }
}
