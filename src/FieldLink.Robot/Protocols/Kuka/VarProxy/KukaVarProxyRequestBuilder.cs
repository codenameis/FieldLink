using System;
using System.Text;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKAVARPROXY 바이너리 읽기·쓰기 요청 프레임을 생성하는 순수 정적 빌더입니다.</summary>
public static class KukaVarProxyRequestBuilder
{
    private const int HeaderLength = 4;
    private const int FunctionCodeLength = 1;
    private const int LengthFieldByteCount = 2;
    private const int BodyVariableNameOffset = 7;

    /// <summary>KRL 변수 읽기(<c>0x00</c>) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildReadRequest(ushort messageId, string variableName, Encoding? encoding = null)
    {
        if (variableName is null)
        {
            throw new ArgumentNullException(nameof(variableName));
        }

        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("변수 이름은 비어 있을 수 없습니다.", nameof(variableName));
        }

        Encoding effectiveEncoding = encoding ?? Encoding.ASCII;
        byte[] nameBytes = effectiveEncoding.GetBytes(variableName);
        int bodyLength = FunctionCodeLength + LengthFieldByteCount + nameBytes.Length;
        if (nameBytes.Length > ushort.MaxValue || bodyLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(variableName), variableName.Length, "KUKAVARPROXY 변수 이름 길이가 16비트 한도를 초과합니다.");
        }

        byte[] frame = new byte[HeaderLength + bodyLength];
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, 0, messageId);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, LengthFieldByteCount, (ushort)bodyLength);
        frame[HeaderLength] = (byte)KukaVarProxyFunctionCode.Read;
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, HeaderLength + FunctionCodeLength, (ushort)nameBytes.Length);
        Array.Copy(nameBytes, 0, frame, BodyVariableNameOffset, nameBytes.Length);
        return frame;
    }

    /// <summary>KRL 변수 쓰기(<c>0x01</c>) 요청 프레임을 생성합니다.</summary>
    public static byte[] BuildWriteRequest(
        ushort messageId,
        string variableName,
        string value,
        Encoding? encoding = null)
    {
        if (variableName is null)
        {
            throw new ArgumentNullException(nameof(variableName));
        }

        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("변수 이름은 비어 있을 수 없습니다.", nameof(variableName));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Encoding effectiveEncoding = encoding ?? Encoding.ASCII;
        byte[] nameBytes = effectiveEncoding.GetBytes(variableName);
        byte[] valueBytes = effectiveEncoding.GetBytes(value);
        int bodyLength = FunctionCodeLength + LengthFieldByteCount + nameBytes.Length + LengthFieldByteCount + valueBytes.Length;
        if (nameBytes.Length > ushort.MaxValue || valueBytes.Length > ushort.MaxValue || bodyLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), bodyLength, "KUKAVARPROXY 쓰기 요청 크기가 16비트 한도를 초과합니다.");
        }

        byte[] frame = new byte[HeaderLength + bodyLength];
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, 0, messageId);
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, LengthFieldByteCount, (ushort)bodyLength);
        frame[HeaderLength] = (byte)KukaVarProxyFunctionCode.Write;
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, HeaderLength + FunctionCodeLength, (ushort)nameBytes.Length);
        Array.Copy(nameBytes, 0, frame, BodyVariableNameOffset, nameBytes.Length);

        int valueLengthOffset = BodyVariableNameOffset + nameBytes.Length;
        RobotBinaryBuffer.WriteUInt16BigEndian(frame, valueLengthOffset, (ushort)valueBytes.Length);
        Array.Copy(valueBytes, 0, frame, valueLengthOffset + LengthFieldByteCount, valueBytes.Length);
        return frame;
    }
}
