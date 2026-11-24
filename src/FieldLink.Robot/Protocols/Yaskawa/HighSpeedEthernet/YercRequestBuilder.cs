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
    private const int Int32ByteSize = 4;
    private const int SelectJobNameMaxBytes = 32;
    private const int SelectJobPayloadLength = 36;
    private const byte Reserve1Value = 0x03;
    private const byte RobotControlDivision = 0x01;
    private const byte RequestAckFlag = 0x00;
    private const string MagicIdentifier = "YERC";
    private const string Reserve2Value = "99999999";

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

    internal static byte[] BuildInt32Payload(int value)
    {
        byte[] payload = new byte[Int32ByteSize];
        RobotBinaryBuffer.WriteInt32LittleEndian(payload, 0, value);
        return payload;
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

    internal static byte[] BuildSelectJobPayload(string jobName, int lineNumber, Encoding encoding)
    {
        if (jobName is null)
        {
            throw new ArgumentNullException(nameof(jobName));
        }

        byte[] payload = new byte[SelectJobPayloadLength];
        byte[] nameBytes = encoding.GetBytes(jobName);
        Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, SelectJobNameMaxBytes));
        RobotBinaryBuffer.WriteInt32LittleEndian(payload, SelectJobNameMaxBytes, lineNumber);
        return payload;
    }
}
