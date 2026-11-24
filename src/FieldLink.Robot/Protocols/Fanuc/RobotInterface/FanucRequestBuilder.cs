using System;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC SRTP/SNPX 요청 프레임을 생성하는 순수 정적 빌더입니다.</summary>
public static class FanucRequestBuilder
{
    private const int HeaderLength = 56;
    private const int InlineMaxBytes = 6;
    private const byte ServiceRead = 0x04;
    private const byte ServiceWrite = 0x07;

    private static readonly byte[] SessionInitTemplate =
    {
        0x08, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0xC0,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x4F, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    private static readonly byte[] ReadTemplate =
    {
        0x02, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0xC0,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, ServiceRead, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    private static readonly byte[] InlineWriteTemplate =
    {
        0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0xC0,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, ServiceWrite, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    private static readonly byte[] ExtendedWriteTemplate =
    {
        0x02, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x80,
        0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x01, ServiceWrite, 0x00, 0x00, 0x00, 0x00, 0x00
    };

    /// <summary>Stage 1 연결 요청 패킷(56바이트)을 생성합니다.</summary>
    public static byte[] BuildConnectRequest(int clientId)
    {
        byte[] frame = new byte[HeaderLength];
        RobotBinaryBuffer.WriteInt32LittleEndian(frame, 1, clientId);
        return frame;
    }

    /// <summary>Stage 2 세션 초기화 요청 패킷(56바이트)을 생성합니다.</summary>
    public static byte[] BuildSessionInitRequest() => (byte[])SessionInitTemplate.Clone();

    /// <summary>읽기 요청 패킷(56바이트)을 생성합니다.</summary>
    public static byte[] BuildReadRequest(FanucSelector selector, ushort address1Based, ushort length)
    {
        if (address1Based == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address1Based), address1Based, "FANUC 주소는 1 이상이어야 합니다.");
        }

        byte[] frame = (byte[])ReadTemplate.Clone();
        frame[43] = (byte)selector;
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, 44, (ushort)(address1Based - 1));
        RobotBinaryBuffer.WriteUInt16LittleEndian(frame, 46, length);
        return frame;
    }

    /// <summary>쓰기 요청 패킷을 생성합니다. (6바이트 이하는 인라인, 초과는 확장 프레임)</summary>
    public static byte[] BuildWriteRequest(FanucSelector selector, ushort address1Based, byte[] value, ushort length)
    {
        if (address1Based == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address1Based), address1Based, "FANUC 주소는 1 이상이어야 합니다.");
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length <= InlineMaxBytes)
        {
            byte[] frame = (byte[])InlineWriteTemplate.Clone();
            frame[43] = (byte)selector;
            RobotBinaryBuffer.WriteUInt16LittleEndian(frame, 44, (ushort)(address1Based - 1));
            RobotBinaryBuffer.WriteUInt16LittleEndian(frame, 46, length);
            Array.Copy(value, 0, frame, 48, value.Length);
            return frame;
        }

        if (value.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value.Length, "FANUC 확장 쓰기 바이트 길이는 65535바이트를 초과할 수 없습니다.");
        }

        byte[] extendedFrame = new byte[HeaderLength + value.Length];
        Array.Copy(ExtendedWriteTemplate, 0, extendedFrame, 0, HeaderLength);
        RobotBinaryBuffer.WriteUInt16LittleEndian(extendedFrame, 4, (ushort)value.Length);
        extendedFrame[51] = (byte)selector;
        RobotBinaryBuffer.WriteUInt16LittleEndian(extendedFrame, 52, (ushort)(address1Based - 1));
        RobotBinaryBuffer.WriteUInt16LittleEndian(extendedFrame, 54, length);
        Array.Copy(value, 0, extendedFrame, HeaderLength, value.Length);
        return extendedFrame;
    }

    internal static (int BitOffset, int ByteLength) CalculateBitAlignment(ushort address1Based, int bitCount)
    {
        int byteStartBit = (((address1Based - 1) / 8) * 8) + 1;
        int endBit = address1Based + bitCount - 1;
        int byteEndBit = endBit % 8 == 0 ? endBit : ((endBit / 8) * 8) + 8;
        int byteLength = (byteEndBit - byteStartBit + 1) / 8;
        int bitOffset = address1Based - byteStartBit;
        return (bitOffset, byteLength);
    }

    internal static byte[] BuildBitWritePayload(ushort address1Based, bool[] values)
    {
        (int bitOffset, int byteLength) = CalculateBitAlignment(address1Based, values.Length);
        bool[] alignedBits = new bool[byteLength * 8];
        Array.Copy(values, 0, alignedBits, bitOffset, values.Length);
        return RobotBitPacking.Pack(alignedBits);
    }
}
