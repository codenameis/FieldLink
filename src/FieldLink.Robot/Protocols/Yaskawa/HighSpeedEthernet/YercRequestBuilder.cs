using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) UDP 요청 패킷을 생성하는 순수 정적 빌더입니다.</summary>
public static class YercRequestBuilder
{
    private const int HeaderLength = 32;
    private const int HeaderSizeOffset = 4;
    private const int DataLengthOffset = 6;
    private const int Reserve1Offset = 8;
    private const int DivisionOffset = 9;
    private const int AckFlagOffset = 10;
    private const int RequestIdOffset = 11;
    private const int Reserve2Offset = 16;
    private const int CommandOffset = 24;
    private const int InstanceOffset = 26;
    private const int AttributeOffset = 28;
    private const int ServiceOffset = 29;
    private const int Int16ByteSize = sizeof(short);
    private const int Int32ByteSize = sizeof(int);
    private const int SingleByteSize = sizeof(float);
    private const int StringVariableLength = 16;
    private const int SelectJobNameMaxBytes = 32;
    private const int SelectJobPayloadLength = 36;
    private const byte Reserve1Value = 0x03;
    private const byte RobotControlDivision = 0x01;
    private const byte RequestAckFlag = 0x00;
    private const string MagicIdentifier = "YERC";
    private const string Reserve2Value = "99999999";

    private static readonly char[] DisallowedJobNameCharacters = { '\r', '\n', '\0' };

    /// <summary>32바이트 <c>YERC</c> 헤더와 선택적 데이터부를 결합해 요청 데이터그램을 생성합니다.</summary>
    public static byte[] BuildRequest(YercCommandHeader header, byte requestId, byte[]? dataPart = null)
    {
        if (header is null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        int dataLength = dataPart?.Length ?? 0;
        if (dataLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(dataPart), dataLength, "YERC 데이터 길이는 65535바이트를 초과할 수 없습니다.");
        }

        byte[] frame = new byte[HeaderLength + dataLength];

        Encoding.ASCII.GetBytes(MagicIdentifier, 0, MagicIdentifier.Length, frame, 0);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, HeaderSizeOffset, HeaderLength);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, DataLengthOffset, (ushort)dataLength);
        frame[Reserve1Offset] = Reserve1Value;
        frame[DivisionOffset] = RobotControlDivision;
        frame[AckFlagOffset] = RequestAckFlag;
        frame[RequestIdOffset] = requestId;
        Encoding.ASCII.GetBytes(Reserve2Value, 0, Reserve2Value.Length, frame, Reserve2Offset);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, CommandOffset, header.Command);
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, InstanceOffset, header.Instance);
        frame[AttributeOffset] = header.Attribute;
        frame[ServiceOffset] = header.Service;

        if (dataPart is { Length: > 0 })
        {
            Array.Copy(dataPart, 0, frame, HeaderLength, dataLength);
        }

        return frame;
    }

    internal static byte[] BuildUInt16Payload(ushort value)
    {
        byte[] bytes = new byte[Int16ByteSize];
        RobotBinaryBuffer.WriteUInt16LittleEndian(bytes, 0, value);
        return bytes;
    }

    internal static byte[] BuildInt32Payload(int value)
    {
        byte[] payload = new byte[Int32ByteSize];
        RobotBinaryBuffer.WriteInt32LittleEndian(payload, 0, value);
        return payload;
    }

    internal static byte[] BuildSinglePayload(float value)
    {
        ValidateFiniteSingle(value, nameof(value));
        byte[] bytes = new byte[SingleByteSize];
        RobotBinaryBuffer.WriteSingleLittleEndian(bytes, 0, value);
        return bytes;
    }

    internal static byte[] BuildPaddedStringPayload(string value, Encoding encoding)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] bytes = encoding.GetBytes(value);
        if (bytes.Length > StringVariableLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                bytes.Length,
                $"YERC 문자열 변수 값은 {StringVariableLength}바이트를 초과할 수 없습니다.");
        }

        byte[] padded = new byte[StringVariableLength];
        Array.Copy(bytes, 0, padded, 0, bytes.Length);
        return padded;
    }

    internal static byte[] BuildBatchWritePayload(byte[] itemBytes, int elementCount, bool includeCountHeader)
    {
        if (!includeCountHeader)
        {
            return (byte[])itemBytes.Clone();
        }

        byte[] payload = new byte[Int32ByteSize + itemBytes.Length];
        RobotBinaryBuffer.WriteInt32LittleEndian(payload, 0, elementCount);
        Array.Copy(itemBytes, 0, payload, Int32ByteSize, itemBytes.Length);
        return payload;
    }

    internal static byte[] BuildUInt16BatchPayload(ushort[] values, bool includeCountHeader)
    {
        byte[] raw = new byte[values.Length * Int16ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            RobotBinaryBuffer.WriteUInt16LittleEndian(raw, i * Int16ByteSize, values[i]);
        }

        return BuildBatchWritePayload(raw, values.Length, includeCountHeader);
    }

    internal static byte[] BuildInt16BatchPayload(short[] values, bool includeCountHeader)
    {
        byte[] raw = new byte[values.Length * Int16ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            RobotBinaryBuffer.WriteInt16LittleEndian(raw, i * Int16ByteSize, values[i]);
        }

        return BuildBatchWritePayload(raw, values.Length, includeCountHeader);
    }

    internal static byte[] BuildInt32BatchPayload(int[] values, bool includeCountHeader)
    {
        byte[] raw = new byte[values.Length * Int32ByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            RobotBinaryBuffer.WriteInt32LittleEndian(raw, i * Int32ByteSize, values[i]);
        }

        return BuildBatchWritePayload(raw, values.Length, includeCountHeader);
    }

    internal static byte[] BuildSingleBatchPayload(float[] values, bool includeCountHeader)
    {
        byte[] raw = new byte[values.Length * SingleByteSize];
        for (int i = 0; i < values.Length; i++)
        {
            ValidateFiniteSingle(values[i], nameof(values));
            RobotBinaryBuffer.WriteSingleLittleEndian(raw, i * SingleByteSize, values[i]);
        }

        return BuildBatchWritePayload(raw, values.Length, includeCountHeader);
    }

    internal static byte[] BuildStringBatchPayload(string[] values, Encoding encoding, bool includeCountHeader)
    {
        byte[] raw = new byte[values.Length * StringVariableLength];
        for (int i = 0; i < values.Length; i++)
        {
            string entry = values[i] ?? throw new ArgumentNullException(nameof(values), $"인덱스 {i}의 문자열이 null입니다.");
            byte[] entryBytes = encoding.GetBytes(entry);
            if (entryBytes.Length > StringVariableLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values),
                    entryBytes.Length,
                    $"인덱스 {i}의 문자열 변수 길이가 {StringVariableLength}바이트를 초과합니다.");
            }

            Array.Copy(entryBytes, 0, raw, i * StringVariableLength, entryBytes.Length);
        }

        return BuildBatchWritePayload(raw, values.Length, includeCountHeader);
    }

    internal static byte[] BuildSelectJobPayload(string jobName, int lineNumber, Encoding encoding)
    {
        if (jobName is null)
        {
            throw new ArgumentNullException(nameof(jobName));
        }

        if (string.IsNullOrWhiteSpace(jobName) || jobName.IndexOfAny(DisallowedJobNameCharacters) >= 0)
        {
            throw new ArgumentException("JOB 이름이 비어 있거나 제어 문자를 포함할 수 없습니다.", nameof(jobName));
        }

        if (lineNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "JOB 라인 번호는 0 이상이어야 합니다.");
        }

        byte[] nameBytes = encoding.GetBytes(jobName);
        if (nameBytes.Length > SelectJobNameMaxBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jobName),
                nameBytes.Length,
                $"JOB 이름은 {SelectJobNameMaxBytes}바이트를 초과할 수 없습니다.");
        }

        byte[] payload = new byte[SelectJobPayloadLength];
        Array.Copy(nameBytes, 0, payload, 0, nameBytes.Length);
        RobotBinaryBuffer.WriteInt32LittleEndian(payload, SelectJobNameMaxBytes, lineNumber);
        return payload;
    }

    private static void ValidateFiniteSingle(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "실수 값은 유한한 숫자여야 합니다.");
        }
    }
}
