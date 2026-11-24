using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 레지스터 <c>0..99</c> 원시 바이트(200바이트)를 상태 스냅샷으로 디코딩하는 순수 정적 파서입니다.</summary>
public static class EstunSnapshotParser
{
    private const int RequiredSnapshotByteLength = 200;
    private const int GlobalSpeedOffset = 2;
    private const int StatusFlagsOffset = 7;
    private const int ProjectNameOffset = 8;
    private const int ProjectNameByteLength = 20;
    private const int DigitalOutputOffset = 28;
    private const int DigitalIoByteLength = 8;
    private const int DigitalIoBitCount = 64;
    private const int RobotCommandStatusOffset = 36;
    private const int AnalogOutputOffset = 38;
    private const int DigitalInputOffset = 126;
    private const int AnalogInputOffset = 134;
    private const int ReadWriteFlagOffset = 198;
    private const int AnalogChannelCount = 16;
    private const int SingleByteSize = sizeof(float);

    private const byte ManualModeMask = 0x01;
    private const byte AutoModeMask = 0x02;
    private const byte RemoteModeMask = 0x04;
    private const byte ServoEnabledMask = 0x08;
    private const byte RunningMask = 0x10;
    private const byte ErrorMask = 0x20;
    private const byte ProgramRunningMask = 0x40;
    private const byte MovingMask = 0x80;

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
                $"Estun 상태 스냅샷 길이가 {RequiredSnapshotByteLength}바이트보다 짧습니다. (실제: {registerBytes.Length} 바이트)",
                registerBytes);
        }

        byte statusFlags = registerBytes[StatusFlagsOffset];
        byte[] projectBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, ProjectNameOffset, ProjectNameByteLength);
        byte[] digitalOutputBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, DigitalOutputOffset, DigitalIoByteLength);
        byte[] digitalInputBytes = RobotBinaryBuffer.ReverseByWord(registerBytes, DigitalInputOffset, DigitalIoByteLength);

        return new EstunRobotSnapshot
        {
            GlobalSpeedValue = RobotBinaryBuffer.ReadInt16BigEndian(registerBytes, GlobalSpeedOffset),
            IsManualMode = (statusFlags & ManualModeMask) != 0,
            IsAutoMode = (statusFlags & AutoModeMask) != 0,
            IsRemoteMode = (statusFlags & RemoteModeMask) != 0,
            IsServoEnabled = (statusFlags & ServoEnabledMask) != 0,
            IsRunning = (statusFlags & RunningMask) != 0,
            HasError = (statusFlags & ErrorMask) != 0,
            IsProgramRunning = (statusFlags & ProgramRunningMask) != 0,
            IsMoving = (statusFlags & MovingMask) != 0,
            ProjectName = RobotBinaryBuffer.ReadNullTrimmedString(projectBytes, 0, ProjectNameByteLength, Encoding.ASCII),
            DigitalOutputs = Array.AsReadOnly(RobotBitPacking.Unpack(digitalOutputBytes, 0, DigitalIoBitCount)),
            DigitalInputs = Array.AsReadOnly(RobotBitPacking.Unpack(digitalInputBytes, 0, DigitalIoBitCount)),
            RobotCommandStatus = RobotBinaryBuffer.ReadUInt16BigEndian(registerBytes, RobotCommandStatusOffset),
            AnalogOutputs = Array.AsReadOnly(ReadAnalogChannels(registerBytes, AnalogOutputOffset)),
            AnalogInputs = Array.AsReadOnly(ReadAnalogChannels(registerBytes, AnalogInputOffset)),
            ReadWriteFlag = RobotBinaryBuffer.ReadInt16BigEndian(registerBytes, ReadWriteFlagOffset)
        };
    }

    private static float[] ReadAnalogChannels(byte[] buffer, int offset)
    {
        float[] channels = new float[AnalogChannelCount];
        for (int i = 0; i < AnalogChannelCount; i++)
        {
            channels[i] = RobotBinaryBuffer.ReadSingleWordSwapBigEndian(buffer, offset + (i * SingleByteSize));
        }

        return channels;
    }
}
