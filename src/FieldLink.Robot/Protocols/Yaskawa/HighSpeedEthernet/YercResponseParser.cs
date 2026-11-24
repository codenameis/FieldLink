using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA <c>YERC</c> UDP 응답 헤더를 검증하고 각 명령별 페이로드를 해석하는 순수 정적 파서입니다.</summary>
public static class YercResponseParser
{
    private const int HeaderLength = 32;
    private const byte MagicByte0 = (byte)'Y';
    private const byte MagicByte1 = (byte)'E';
    private const byte MagicByte2 = (byte)'R';
    private const byte MagicByte3 = (byte)'C';
    private const int RequestIdOffset = 11;
    private const int StatusOffset = 25;
    private const int AddedStatusSizeOffset = 26;
    private const int AddedStatusOffset = 28;
    private const byte SingleByteAddedStatusSize = 0x01;
    private const byte SuccessStatus = 0x00;
    private const byte ExtendedErrorStatus = 0x1F;
    private const int StringVariableLength = 16;
    private const int AlarmHeaderLength = 32;
    private const int AlarmTimeOffset = 16;
    private const int AlarmTimeLength = 16;
    private const int StatusBitsPayloadLength = 8;
    private const int StatusBitsHighByteOffset = 4;
    private const int StatusBitCount = 16;
    private const int JobSequenceMinimumLength = 44;
    private const int JobNameLength = 32;
    private const int PoseHeaderLength = 20;
    private const int SystemInfoMinimumLength = 48;
    private const int SoftwareVersionLength = 24;
    private const int ModelNameOffset = 24;
    private const int ModelNameLength = 16;
    private const int ParameterVersionOffset = 40;
    private const int ParameterVersionLength = 8;
    private const int BatchCountHeaderLength = 4;
    private const int Int16ByteSize = sizeof(short);
    private const int Int32ByteSize = sizeof(int);
    private const int SingleByteSize = sizeof(float);

    /// <summary>32바이트 <c>YERC</c> 응답 헤더와 상태 코드를 검증한 뒤 가변 페이로드 바이트를 추출합니다.</summary>
    public static byte[] ExtractPayload(byte[] response, byte? expectedRequestId = null)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (response.Length < HeaderLength)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"YERC 응답 길이가 헤더 크기({HeaderLength}바이트)보다 짧습니다. (실제: {response.Length}바이트)",
                response);
        }

        if (response[0] != MagicByte0 || response[1] != MagicByte1 || response[2] != MagicByte2 || response[3] != MagicByte3)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, "YERC 응답 매직 헤더가 일치하지 않습니다.", response);
        }

        if (expectedRequestId.HasValue && response[RequestIdOffset] != expectedRequestId.Value)
        {
            throw new RobotException(
                RobotErrorKind.InvalidResponse,
                $"YERC RequestID가 일치하지 않습니다. (기대: {expectedRequestId.Value}, 수신: {response[RequestIdOffset]})",
                response);
        }

        byte status = response[StatusOffset];
        if (status != SuccessStatus)
        {
            ushort addedStatus = response[AddedStatusSizeOffset] == SingleByteAddedStatusSize
                ? response[AddedStatusOffset]
                : RobotBinaryBuffer.ReadUInt16LittleEndian(response, AddedStatusOffset);
            int errorCode = status == ExtendedErrorStatus ? addedStatus : status;
            throw new RobotDeviceException(errorCode, YercStatusCode.GetDescription(status, addedStatus), response);
        }

        int payloadLength = response.Length - HeaderLength;
        byte[] payload = new byte[payloadLength];
        if (payloadLength > 0)
        {
            Array.Copy(response, HeaderLength, payload, 0, payloadLength);
        }

        return payload;
    }

    /// <summary>알람 응답 페이로드(<c>0x70</c>/<c>0x71</c>)를 해석합니다. 비어 있으면 null을 반환합니다.</summary>
    public static YercAlarmItem? ParseAlarm(byte[] payload, Encoding encoding)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length == 0)
        {
            return null;
        }

        if (payload.Length < AlarmHeaderLength)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, $"YERC 알람 페이로드가 {AlarmHeaderLength}바이트보다 짧습니다.", payload);
        }

        return new YercAlarmItem
        {
            AlarmCode = RobotBinaryBuffer.ReadInt32LittleEndian(payload, 0),
            AlarmData = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize),
            AlarmType = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize * 2),
            Time = RobotBinaryBuffer.ReadNullTrimmedString(payload, AlarmTimeOffset, AlarmTimeLength, Encoding.ASCII),
            Message = RobotBinaryBuffer.ReadNullTrimmedString(payload, AlarmHeaderLength, payload.Length - AlarmHeaderLength, encoding)
        };
    }

    /// <summary>상태 비트 페이로드(<c>0x72</c>)를 16비트 불리언 배열로 해석합니다.</summary>
    public static bool[] ParseStatusBits(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length < StatusBitsPayloadLength)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, $"YERC 상태 비트 페이로드가 {StatusBitsPayloadLength}바이트보다 짧습니다.", payload);
        }

        byte lowByte = payload[0];
        byte highByte = payload[StatusBitsHighByteOffset];
        return RobotBitPacking.Unpack(new[] { lowByte, highByte }, 0, StatusBitCount);
    }

    /// <summary>실행 JOB 정보 페이로드(<c>0x73</c>)를 해석합니다.</summary>
    public static YercJobSequence ParseJobSequence(byte[] payload, Encoding encoding)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length < JobSequenceMinimumLength)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, $"YERC JOB 정보 페이로드가 {JobSequenceMinimumLength}바이트보다 짧습니다.", payload);
        }

        return new YercJobSequence
        {
            JobName = RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, JobNameLength, encoding),
            LineNumber = RobotBinaryBuffer.ReadInt32LittleEndian(payload, JobNameLength),
            StepNumber = RobotBinaryBuffer.ReadInt32LittleEndian(payload, JobNameLength + Int32ByteSize),
            SpeedOverride = RobotBinaryBuffer.ReadInt32LittleEndian(payload, JobNameLength + (Int32ByteSize * 2))
        };
    }

    /// <summary>현재 자세 페이로드(<c>0x75</c>)를 해석합니다.</summary>
    public static YercPose ParsePose(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length < PoseHeaderLength || (payload.Length - PoseHeaderLength) % Int32ByteSize != 0)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, "YERC 자세 페이로드 길이가 올바르지 않습니다.", payload);
        }

        int axisCount = (payload.Length - PoseHeaderLength) / Int32ByteSize;
        return new YercPose
        {
            DataType = RobotBinaryBuffer.ReadInt32LittleEndian(payload, 0),
            Form = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize),
            ToolNumber = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize * 2),
            UserCoordinateNumber = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize * 3),
            ExtendedForm = RobotBinaryBuffer.ReadInt32LittleEndian(payload, Int32ByteSize * 4),
            Coordinates = Array.AsReadOnly(ReadInt32Array(payload, PoseHeaderLength, axisCount))
        };
    }

    /// <summary>토크 응답 페이로드(<c>0x77</c>)를 32비트 정수 배열로 해석합니다.</summary>
    public static int[] ParseTorque(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length % Int32ByteSize != 0)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, "YERC 토크 페이로드 길이가 4의 배수가 아닙니다.", payload);
        }

        return ReadInt32Array(payload, 0, payload.Length / Int32ByteSize);
    }

    /// <summary>시스템 정보 페이로드(<c>0x89</c>)를 해석합니다.</summary>
    public static YercSystemInfo ParseSystemInfo(byte[] payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length < SystemInfoMinimumLength)
        {
            throw new RobotException(RobotErrorKind.InvalidResponse, $"YERC 시스템 정보 페이로드가 {SystemInfoMinimumLength}바이트보다 짧습니다.", payload);
        }

        return new YercSystemInfo
        {
            SoftwareVersion = RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, SoftwareVersionLength, Encoding.ASCII),
            ModelName = RobotBinaryBuffer.ReadNullTrimmedString(payload, ModelNameOffset, ModelNameLength, Encoding.ASCII),
            ParameterVersion = RobotBinaryBuffer.ReadNullTrimmedString(payload, ParameterVersionOffset, ParameterVersionLength, Encoding.ASCII)
        };
    }

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

    private static int[] ReadInt32Array(byte[] payload, int offset, int count)
    {
        int[] values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = RobotBinaryBuffer.ReadInt32LittleEndian(payload, offset + (i * Int32ByteSize));
        }

        return values;
    }
}
