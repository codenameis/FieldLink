using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 레지스터 <c>0..99</c> 원시 바이트(200바이트)를 상태 스냅샷으로 디코딩하는 순수 정적 파서입니다.</summary>
public static class EstunSnapshotParser
{
    private const int RequiredSnapshotByteLength = 200;
    private const int ProjectNameByteLength = 20;
    private const int DigitalIoByteLength = 8;
    private const int DigitalIoBitCount = 64;
    private const int AnalogChannelCount = 16;

    /// <summary>200바이트 상태 레지스터 블록을 <see cref="EstunRobotSnapshot"/>으로 파싱합니다.</summary>
    /// <param name="registerBytes">레지스터 0~99에서 읽은 200바이트 배열입니다.</param>
    /// <returns>해석된 스냅샷입니다.</returns>
    public static EstunRobotSnapshot ParseSnapshot(byte[] registerBytes)
    {
        if (registerBytes is null)
        {
            throw new ArgumentNullException(nameof(registerBytes));
        }

        if (registerBytes.Length < RequiredSnapshotByteLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"Estun 상태 스냅샷 길이가 200바이트보다 짧습니다. (실제: {registerBytes.Length} 바이트)",
                registerBytes);
        }

        byte statusFlags = registerBytes[7];
        byte[] projectBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, 8, ProjectNameByteLength);
        byte[] digitalOutputBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, 28, DigitalIoByteLength);
        byte[] digitalInputBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, 126, DigitalIoByteLength);

        return new EstunRobotSnapshot
        {
            GlobalSpeedValue = RobotBinaryBuffer.ReadInt16BigEndian(registerBytes, 2),
            IsManualMode = (statusFlags & 0x01) != 0,
            IsAutoMode = (statusFlags & 0x02) != 0,
            IsRemoteMode = (statusFlags & 0x04) != 0,
            IsServoEnabled = (statusFlags & 0x08) != 0,
            IsRunning = (statusFlags & 0x10) != 0,
            HasError = (statusFlags & 0x20) != 0,
            IsProgramRunning = (statusFlags & 0x40) != 0,
            IsMoving = (statusFlags & 0x80) != 0,
            ProjectName = RobotBinaryBuffer.ReadNullTrimmedString(projectBytes, 0, ProjectNameByteLength, Encoding.ASCII),
            DigitalOutputs = Array.AsReadOnly(RobotBitPacking.Unpack(digitalOutputBytes, 0, DigitalIoBitCount)),
            DigitalInputs = Array.AsReadOnly(RobotBitPacking.Unpack(digitalInputBytes, 0, DigitalIoBitCount)),
            RobotCommandStatus = RobotBinaryBuffer.ReadUInt16BigEndian(registerBytes, 36),
            AnalogOutputs = Array.AsReadOnly(ReadAnalogChannels(registerBytes, 38)),
            AnalogInputs = Array.AsReadOnly(ReadAnalogChannels(registerBytes, 134)),
            ReadWriteFlag = RobotBinaryBuffer.ReadInt16BigEndian(registerBytes, 198)
        };
    }

    private static float[] ReadAnalogChannels(byte[] buffer, int offset)
    {
        float[] channels = new float[AnalogChannelCount];
        for (int i = 0; i < AnalogChannelCount; i++)
        {
            channels[i] = RobotBinaryBuffer.ReadSingleWordSwapBigEndian(buffer, offset + (i * 4));
        }

        return channels;
    }
}
