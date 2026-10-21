using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class YokogawaRequestBuilder
{
    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (명령 0x11 / 0x01)</summary>
    public static byte[] BuildReadRequest(byte cpuNumber, YokogawaAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "개수는 1 이상이어야 합니다.");
        }

        byte command = address.IsBit ? (byte)0x01 : (byte)0x11;
        byte[] frame = new byte[12];
        WriteHeaderAndAddress(frame, command, cpuNumber, paramSize: 8, address, count);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (명령 0x12)</summary>
    public static byte[] BuildWriteWordRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] wordBytes)
    {
        return BuildWriteFrame(command: 0x12, cpuNumber, address, count, wordBytes);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (명령 0x02)</summary>
    public static byte[] BuildWriteBitRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] bitBytes)
    {
        return BuildWriteFrame(command: 0x02, cpuNumber, address, count, bitBytes);
    }

    private static byte[] BuildWriteFrame(byte command, byte cpuNumber, YokogawaAddress address, ushort count, byte[] payload)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        ushort paramSize = (ushort)(8 + payload.Length);
        byte[] frame = new byte[4 + paramSize];
        WriteHeaderAndAddress(frame, command, cpuNumber, paramSize, address, count);
        Buffer.BlockCopy(payload, 0, frame, 12, payload.Length);
        return frame;
    }

    private static void WriteHeaderAndAddress(
        byte[] frame,
        byte command,
        byte cpuNumber,
        ushort paramSize,
        YokogawaAddress address,
        ushort count)
    {
        frame[0] = command;
        frame[1] = cpuNumber;
        BinaryBuffer.WriteUInt16BE(frame, 2, paramSize);
        BinaryBuffer.WriteUInt16BE(frame, 4, address.Attribute);
        BinaryBuffer.WriteUInt32BE(frame, 6, (uint)address.Number);
        BinaryBuffer.WriteUInt16BE(frame, 10, count);
    }
}
