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
        ushort instructionLength = (ushort)(12 + varBytes.Length);
        byte[] frame = new byte[20 + instructionLength];

        BuildHeader(frame, invokeId, instructionLength, cpuInfo);

        int offset = 20;
        BinaryBuffer.WriteUInt16LE(frame, offset, CommandRead);
        BinaryBuffer.WriteUInt16LE(frame, offset + 2, DataTypeContinuous);
        BinaryBuffer.WriteUInt16LE(frame, offset + 4, 0x0000); // Reserved
        BinaryBuffer.WriteUInt16LE(frame, offset + 6, 0x0001); // 1 block
        BinaryBuffer.WriteUInt16LE(frame, offset + 8, (ushort)varBytes.Length);
        Buffer.BlockCopy(varBytes, 0, frame, offset + 10, varBytes.Length);
        BinaryBuffer.WriteUInt16LE(frame, offset + 10 + varBytes.Length, byteCount);

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
        ushort instructionLength = (ushort)(12 + varBytes.Length + data.Length);
        byte[] frame = new byte[20 + instructionLength];

        BuildHeader(frame, invokeId, instructionLength, cpuInfo);

        int offset = 20;
        BinaryBuffer.WriteUInt16LE(frame, offset, CommandWrite);
        BinaryBuffer.WriteUInt16LE(frame, offset + 2, DataTypeContinuous);
        BinaryBuffer.WriteUInt16LE(frame, offset + 4, 0x0000); // Reserved
        BinaryBuffer.WriteUInt16LE(frame, offset + 6, 0x0001); // 1 block
        BinaryBuffer.WriteUInt16LE(frame, offset + 8, (ushort)varBytes.Length);
        Buffer.BlockCopy(varBytes, 0, frame, offset + 10, varBytes.Length);
        BinaryBuffer.WriteUInt16LE(frame, offset + 10 + varBytes.Length, (ushort)data.Length);
        Buffer.BlockCopy(data, 0, frame, offset + 12 + varBytes.Length, data.Length);

        return frame;
    }

    private static void BuildHeader(byte[] buffer, ushort invokeId, ushort instructionLength, byte cpuInfo)
    {
        Buffer.BlockCopy(CompanyId, 0, buffer, 0, 8);
        // Offset 8~9: CompanyId Null Padding (0x0000), Offset 10~11: PLC Info (0x0000)
        buffer[12] = cpuInfo;
        buffer[13] = SourceClient;
        BinaryBuffer.WriteUInt16LE(buffer, 14, invokeId);
        BinaryBuffer.WriteUInt16LE(buffer, 16, instructionLength);
        buffer[18] = 0x00; // FEnet Position

        byte bcc = 0;
        for (int i = 0; i < 19; i++)
        {
            bcc = (byte)(bcc + buffer[i]);
        }

        buffer[19] = bcc;
    }
}
