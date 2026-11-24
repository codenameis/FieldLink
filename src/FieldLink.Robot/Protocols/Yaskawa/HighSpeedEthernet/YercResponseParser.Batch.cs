using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

public static partial class YercResponseParser
{
    private const int StringVariableLength = 16;
    private const int BatchCountHeaderLength = 4;
    private const int Int16ByteSize = sizeof(short);
    private const int SingleByteSize = sizeof(float);

    internal static byte ParseSingleBytePayload(byte[] payload)
    {
        EnsureMinimumPayloadLength(payload, sizeof(byte));
        return payload[0];
    }

    internal static ushort ParseUInt16Payload(byte[] payload)
    {
        EnsureMinimumPayloadLength(payload, Int16ByteSize);
        return RobotBinaryBuffer.ReadUInt16LittleEndian(payload, 0);
    }

    internal static short ParseInt16Payload(byte[] payload)
    {
        EnsureMinimumPayloadLength(payload, Int16ByteSize);
        return RobotBinaryBuffer.ReadInt16LittleEndian(payload, 0);
    }

    internal static int ParseInt32Payload(byte[] payload)
    {
        EnsureMinimumPayloadLength(payload, Int32ByteSize);
        return RobotBinaryBuffer.ReadInt32LittleEndian(payload, 0);
    }

    internal static float ParseSinglePayload(byte[] payload)
    {
        EnsureMinimumPayloadLength(payload, SingleByteSize);
        return RobotBinaryBuffer.ReadSingleLittleEndian(payload, 0);
    }

    internal static ushort[] ParseRegisterBatch(byte[] payload, int count, bool hasCountHeader)
    {
        int offset = GetBatchDataOffset(payload, checked(count * Int16ByteSize), hasCountHeader);
        ushort[] result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadUInt16LittleEndian(payload, offset + (i * Int16ByteSize));
        }

        return result;
    }

    internal static short[] ParseInt16Batch(byte[] payload, int count, bool hasCountHeader)
    {
        int offset = GetBatchDataOffset(payload, checked(count * Int16ByteSize), hasCountHeader);
        short[] result = new short[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadInt16LittleEndian(payload, offset + (i * Int16ByteSize));
        }

        return result;
    }

    internal static int[] ParseInt32Batch(byte[] payload, int count, bool hasCountHeader)
    {
        int offset = GetBatchDataOffset(payload, checked(count * Int32ByteSize), hasCountHeader);
        return ReadInt32Array(payload, offset, count);
    }

    internal static float[] ParseSingleBatch(byte[] payload, int count, bool hasCountHeader)
    {
        int offset = GetBatchDataOffset(payload, checked(count * SingleByteSize), hasCountHeader);
        float[] result = new float[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadSingleLittleEndian(payload, offset + (i * SingleByteSize));
        }

        return result;
    }

    internal static byte[] ParseByteBatch(byte[] payload, int count, bool hasCountHeader)
    {
        int offset = GetBatchDataOffset(payload, count, hasCountHeader);
        byte[] result = new byte[count];
        Array.Copy(payload, offset, result, 0, count);
        return result;
    }

    internal static string[] ParseStringBatch(byte[] payload, int count, bool hasCountHeader, Encoding encoding)
    {
        int offset = GetBatchDataOffset(payload, checked(count * StringVariableLength), hasCountHeader);
        string[] result = new string[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RobotBinaryBuffer.ReadNullTrimmedString(payload, offset + (i * StringVariableLength), StringVariableLength, encoding);
        }

        return result;
    }

    internal static int GetBatchDataOffset(byte[] payload, int expectedBodyBytes, bool hasCountHeader)
    {
        if (expectedBodyBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedBodyBytes), expectedBodyBytes, "기대 본문 바이트 수는 0 이상이어야 합니다.");
        }

        int offset = hasCountHeader ? BatchCountHeaderLength : 0;
        if (payload.Length - offset < expectedBodyBytes)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"YERC 배치 응답 길이({payload.Length})가 기대 크기({offset + expectedBodyBytes})보다 짧습니다.",
                payload);
        }

        return offset;
    }

    private static void EnsureMinimumPayloadLength(byte[] payload, int minimumBytes)
    {
        if (payload.Length < minimumBytes)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"YERC 응답 페이로드 길이({payload.Length}바이트)가 기대 최소 크기({minimumBytes}바이트)보다 짧습니다.",
                payload);
        }
    }
}
