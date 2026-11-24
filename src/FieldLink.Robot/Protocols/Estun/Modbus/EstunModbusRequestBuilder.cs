using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun Modbus TCP(MBAP) 요청 프레임을 생성하는 순수 정적 빌더입니다.</summary>
public static class EstunModbusRequestBuilder
{
    private const int MbapHeaderLength = 7;
    private const ushort ProtocolIdentifier = 0x0000;
    private const byte ReadHoldingRegistersFunctionCode = 0x03;
    private const byte WriteSingleRegisterFunctionCode = 0x06;
    private const byte WriteMultipleRegistersFunctionCode = 0x10;
    private const ushort MaxReadRegisterCount = 125;
    private const int MaxWriteRegisterByteCount = 246;
    private const int FixedRequestFrameLength = 12;
    private const ushort FixedRequestMbapLength = 6;
    private const int WriteMultipleMbapOverhead = 7;
    private const int WriteMultiplePduHeaderLength = 6;
    private const int StartAddressOffset = 8;
    private const int QuantityOrValueOffset = 10;
    private const int WriteMultipleByteCountOffset = 12;
    private const int WriteMultipleDataOffset = 13;
    private const int ProjectNameByteLength = 20;
    private const int BytesPerRegister = 2;
    private const char MaximumAsciiCharacter = '\x7F';

    private static readonly char[] DisallowedControlCharacters = { '\r', '\n', '\0' };

    /// <summary>Holding Register 읽기(<c>FC 0x03</c>) Modbus TCP 프레임을 생성합니다.</summary>
    public static byte[] BuildReadHoldingRegisters(
        ushort transactionId,
        byte station,
        ushort startAddress,
        ushort quantity)
    {
        if (quantity == 0 || quantity > MaxReadRegisterCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                $"읽을 레지스터 개수는 1에서 {MaxReadRegisterCount} 사이여야 합니다.");
        }

        byte[] frame = new byte[FixedRequestFrameLength];
        WriteMbapHeader(frame, transactionId, FixedRequestMbapLength, station, ReadHoldingRegistersFunctionCode);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, StartAddressOffset, startAddress);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, QuantityOrValueOffset, quantity);
        return frame;
    }

    /// <summary>단일 레지스터 쓰기(<c>FC 0x06</c>) Modbus TCP 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteSingleRegister(
        ushort transactionId,
        byte station,
        ushort registerAddress,
        short value)
    {
        byte[] frame = new byte[FixedRequestFrameLength];
        WriteMbapHeader(frame, transactionId, FixedRequestMbapLength, station, WriteSingleRegisterFunctionCode);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, StartAddressOffset, registerAddress);
        RobotBinaryBuffer.WriteInt16BigEndian(frame, QuantityOrValueOffset, value);
        return frame;
    }

    /// <summary>다중 레지스터 쓰기(<c>FC 0x10</c>) Modbus TCP 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteMultipleRegisters(
        ushort transactionId,
        byte station,
        ushort startAddress,
        byte[] registerBytes)
    {
        if (registerBytes is null)
        {
            throw new ArgumentNullException(nameof(registerBytes));
        }

        if (registerBytes.Length == 0 || registerBytes.Length % BytesPerRegister != 0 || registerBytes.Length > MaxWriteRegisterByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(registerBytes),
                registerBytes.Length,
                $"레지스터 데이터 길이는 {BytesPerRegister}에서 {MaxWriteRegisterByteCount} 사이의 짝수 바이트여야 합니다.");
        }

        ushort quantity = (ushort)(registerBytes.Length / BytesPerRegister);
        ushort mbapLength = (ushort)(WriteMultipleMbapOverhead + registerBytes.Length);
        byte[] frame = new byte[MbapHeaderLength + WriteMultiplePduHeaderLength + registerBytes.Length];
        WriteMbapHeader(frame, transactionId, mbapLength, station, WriteMultipleRegistersFunctionCode);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, StartAddressOffset, startAddress);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, QuantityOrValueOffset, quantity);
        frame[WriteMultipleByteCountOffset] = (byte)registerBytes.Length;
        Array.Copy(registerBytes, 0, frame, WriteMultipleDataOffset, registerBytes.Length);
        return frame;
    }

    internal static byte[] BuildProjectNamePayload(string projectName, bool swapWords)
    {
        if (projectName is null)
        {
            throw new ArgumentNullException(nameof(projectName));
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("프로젝트 이름은 비어 있을 수 없습니다.", nameof(projectName));
        }

        if (projectName.IndexOfAny(DisallowedControlCharacters) >= 0)
        {
            throw new ArgumentException("프로젝트 이름에 개행 또는 널 문자를 포함할 수 없습니다.", nameof(projectName));
        }

        for (int i = 0; i < projectName.Length; i++)
        {
            if (projectName[i] > MaximumAsciiCharacter)
            {
                throw new ArgumentException("프로젝트 이름은 7비트 ASCII 문자만 사용할 수 있습니다.", nameof(projectName));
            }
        }

        if (projectName.Length > ProjectNameByteLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectName),
                projectName.Length,
                $"Estun 프로젝트 이름은 최대 {ProjectNameByteLength}바이트 ASCII여야 합니다.");
        }

        byte[] raw = new byte[ProjectNameByteLength];
        Encoding.ASCII.GetBytes(projectName, 0, projectName.Length, raw, 0);

        return swapWords ? RobotBinaryBuffer.ReverseByWord(raw, 0, raw.Length) : raw;
    }

    private static void WriteMbapHeader(byte[] frame, ushort transactionId, ushort length, byte station, byte functionCode)
    {
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, 0, transactionId);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, 2, ProtocolIdentifier);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, 4, length);
        frame[6] = station;
        frame[7] = functionCode;
    }
}
