using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 바이너리 링크 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class YokogawaRequestBuilder
{
    private const byte CommandReadBit = 0x01;
    private const byte CommandWriteBit = 0x02;
    private const byte CommandReadWord = 0x11;
    private const byte CommandWriteWord = 0x12;
    private const int HeaderLength = 4;
    private const int AddressParamSize = 8;
    private const int ReadFrameLength = HeaderLength + AddressParamSize;
    private const int CommandOffset = 0;
    private const int CpuNumberOffset = 1;
    private const int ParamSizeOffset = 2;
    private const int AttributeOffset = 4;
    private const int NumberOffset = 6;
    private const int CountOffset = 10;
    private const int PayloadOffset = 12;
    private const int BytesPerWord = 2;

    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (명령 0x11 / 0x01)</summary>
    public static byte[] BuildReadRequest(byte cpuNumber, YokogawaAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        byte command = address.IsBit ? CommandReadBit : CommandReadWord;
        byte[] frame = new byte[ReadFrameLength];
        WriteHeaderAndAddress(frame, command, cpuNumber, AddressParamSize, address, count);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (명령 0x12)</summary>
    public static byte[] BuildWriteWordRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] wordBytes)
    {
        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (wordBytes is null)
        {
            throw new ArgumentNullException(nameof(wordBytes));
        }

        if (wordBytes.Length != count * BytesPerWord)
        {
            throw new ArgumentException($"워드 바이트 길이({wordBytes.Length})가 개수({count})와 일치하지 않습니다.", nameof(wordBytes));
        }

        return BuildWriteFrame(CommandWriteWord, cpuNumber, address, count, wordBytes);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (명령 0x02)</summary>
    public static byte[] BuildWriteBitRequest(byte cpuNumber, YokogawaAddress address, ushort count, byte[] bitBytes)
    {
        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (bitBytes is null)
        {
            throw new ArgumentNullException(nameof(bitBytes));
        }

        if (bitBytes.Length != count)
        {
            throw new ArgumentException($"비트 바이트 길이({bitBytes.Length})가 개수({count})와 일치하지 않습니다.", nameof(bitBytes));
        }

        return BuildWriteFrame(CommandWriteBit, cpuNumber, address, count, bitBytes);
    }

    private static byte[] BuildWriteFrame(byte command, byte cpuNumber, YokogawaAddress address, ushort count, byte[] payload)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ushort paramSize = checked((ushort)(AddressParamSize + payload.Length));
        byte[] frame = new byte[HeaderLength + paramSize];
        WriteHeaderAndAddress(frame, command, cpuNumber, paramSize, address, count);
        Buffer.BlockCopy(payload, 0, frame, PayloadOffset, payload.Length);
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
        frame[CommandOffset] = command;
        frame[CpuNumberOffset] = cpuNumber;
        BinaryBuffer.WriteUInt16BE(frame, ParamSizeOffset, paramSize);
        BinaryBuffer.WriteUInt16BE(frame, AttributeOffset, address.Attribute);
        BinaryBuffer.WriteUInt32BE(frame, NumberOffset, (uint)address.Number);
        BinaryBuffer.WriteUInt16BE(frame, CountOffset, count);
    }
}
