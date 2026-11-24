using System;
using System.Text;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT FEnet 전용 통신 요청 프레임을 생성하는 빌더입니다. (사양서 §3)</summary>
public static class FEnetRequestBuilder
{
    private static readonly byte[] CompanyId = Encoding.ASCII.GetBytes("LSIS-XGT"); // 8 bytes
    private const byte DefaultCpuInfo = 0xA0; // XGK/XGB 기본
    private const byte SourceClient = 0x33;   // Client -> Server

    private const ushort CommandRead = 0x0054;
    private const ushort CommandWrite = 0x0058;
    private const ushort DataTypeContinuous = 0x0014;
    private const ushort ReservedZero = 0x0000;
    private const ushort SingleBlockCount = 0x0001;
    private const byte DefaultPosition = 0x00;

    private const int HeaderLength = 20;
    private const int CompanyIdLength = 8;
    private const int CpuInfoOffset = 12;
    private const int SourceOffset = 13;
    private const int InvokeIdOffset = 14;
    private const int InstructionLengthOffset = 16;
    private const int FEnetPositionOffset = 18;
    private const int HeaderChecksumOffset = 19;

    private const int FixedInstructionHeaderLength = 12;
    private const int DataTypeFieldOffset = 2;
    private const int ReservedFieldOffset = 4;
    private const int BlockCountFieldOffset = 6;
    private const int VariableNameLengthFieldOffset = 8;
    private const int VariableNameFieldOffset = 10;

    /// <summary>연속 읽기 요청 프레임 전체(20바이트 헤더 + 명령어)를 빌드합니다.</summary>
    /// <param name="invokeId">시퀀스 트랜잭션 번호입니다.</param>
    /// <param name="variableName">대상 바이트 변수명 문자열입니다. (예: "%DB200")</param>
    /// <param name="byteCount">읽을 바이트 수입니다.</param>
    /// <param name="cpuInfo">CPU 모델 정보 바이트입니다. 기본값은 0xA0입니다.</param>
    public static byte[] BuildContinuousRead(ushort invokeId, string variableName, ushort byteCount, byte cpuInfo = DefaultCpuInfo)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("변수명은 비어 있을 수 없습니다.", nameof(variableName));
        }

        if (byteCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), "읽을 바이트 수는 1 이상이어야 합니다.");
        }

        byte[] varBytes = Encoding.ASCII.GetBytes(variableName);
        if (varBytes.Length > ushort.MaxValue - FixedInstructionHeaderLength)
        {
            throw new ArgumentOutOfRangeException(nameof(variableName), variableName.Length, "변수명 길이가 최대 FEnet 명령어 길이를 초과합니다.");
        }

        ushort instructionLength = (ushort)(FixedInstructionHeaderLength + varBytes.Length);
        byte[] frame = new byte[HeaderLength + instructionLength];

        BuildHeader(frame, invokeId, instructionLength, cpuInfo);

        int offset = HeaderLength;
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset, CommandRead);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + DataTypeFieldOffset, DataTypeContinuous);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + ReservedFieldOffset, ReservedZero);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + BlockCountFieldOffset, SingleBlockCount);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + VariableNameLengthFieldOffset, (ushort)varBytes.Length);
        Buffer.BlockCopy(varBytes, 0, frame, offset + VariableNameFieldOffset, varBytes.Length);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + VariableNameFieldOffset + varBytes.Length, byteCount);

        return frame;
    }

    /// <summary>연속 쓰기 요청 프레임 전체(20바이트 헤더 + 명령어 + 데이터)를 빌드합니다.</summary>
    /// <param name="invokeId">시퀀스 트랜잭션 번호입니다.</param>
    /// <param name="variableName">대상 바이트 변수명 문자열입니다. (예: "%DB200")</param>
    /// <param name="data">기록할 바이트 데이터입니다.</param>
    /// <param name="cpuInfo">CPU 모델 정보 바이트입니다. 기본값은 0xA0입니다.</param>
    public static byte[] BuildContinuousWrite(ushort invokeId, string variableName, byte[] data, byte cpuInfo = DefaultCpuInfo)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("변수명은 비어 있을 수 없습니다.", nameof(variableName));
        }

        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("쓸 데이터가 비어 있습니다.", nameof(data));
        }

        byte[] varBytes = Encoding.ASCII.GetBytes(variableName);
        if (FixedInstructionHeaderLength + (long)varBytes.Length + data.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "쓰기 데이터 길이가 최대 FEnet 명령어 길이를 초과합니다.");
        }

        ushort instructionLength = (ushort)(FixedInstructionHeaderLength + varBytes.Length + data.Length);
        byte[] frame = new byte[HeaderLength + instructionLength];

        BuildHeader(frame, invokeId, instructionLength, cpuInfo);

        int offset = HeaderLength;
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset, CommandWrite);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + DataTypeFieldOffset, DataTypeContinuous);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + ReservedFieldOffset, ReservedZero);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + BlockCountFieldOffset, SingleBlockCount);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + VariableNameLengthFieldOffset, (ushort)varBytes.Length);
        Buffer.BlockCopy(varBytes, 0, frame, offset + VariableNameFieldOffset, varBytes.Length);
        BinaryBuffer.WriteUInt16LittleEndian(frame, offset + VariableNameFieldOffset + varBytes.Length, (ushort)data.Length);
        Buffer.BlockCopy(data, 0, frame, offset + FixedInstructionHeaderLength + varBytes.Length, data.Length);

        return frame;
    }

    private static void BuildHeader(byte[] buffer, ushort invokeId, ushort instructionLength, byte cpuInfo)
    {
        Buffer.BlockCopy(CompanyId, 0, buffer, 0, CompanyIdLength);
        // Offset 8~9: CompanyId Null Padding (0x0000), Offset 10~11: PLC Info (0x0000)
        buffer[CpuInfoOffset] = cpuInfo;
        buffer[SourceOffset] = SourceClient;
        BinaryBuffer.WriteUInt16LittleEndian(buffer, InvokeIdOffset, invokeId);
        BinaryBuffer.WriteUInt16LittleEndian(buffer, InstructionLengthOffset, instructionLength);
        buffer[FEnetPositionOffset] = DefaultPosition;

        byte bcc = 0;
        for (int i = 0; i < HeaderChecksumOffset; i++)
        {
            bcc = (byte)(bcc + buffer[i]);
        }

        buffer[HeaderChecksumOffset] = bcc;
    }
}
