using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 요청 프레임을 생성하는 빌더입니다. (사양서 Part C §C.1.1)</summary>
public static class FujiElectricSphRequestBuilder
{
    private const byte FrameHeader = 0xFB;
    private const byte FrameType = 0x7B;
    private const int MinWordCount = 1;
    private const int MaxWordCount = 230;
    private const int BytesPerWord = 2;
    private const int HeaderLength = 20;
    private const int DescriptorLength = 6;
    private const int ReadFrameLength = HeaderLength + DescriptorLength;
    private const int LengthFieldOffset = 18;
    private const byte CommandReadWords = 0x00;
    private const byte CommandWriteWords = 0x01;
    private const byte ByteMask = 0xFF;
    private const int MidByteShift = 8;
    private const int HighByteShift = 16;

    /// <summary>연속 워드 읽기 요청 프레임을 조립합니다.</summary>
    public static byte[] BuildReadWords(byte connectionId, FujiElectricSphAddress address, ushort wordCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), $"워드 읽기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        byte[] frame = new byte[ReadFrameLength];
        FillHeader(frame, connectionId, command: CommandReadWords, payloadLength: DescriptorLength);
        FillDescriptor(frame, address, wordCount);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 조립합니다.</summary>
    public static byte[] BuildWriteWords(byte connectionId, FujiElectricSphAddress address, ushort wordCount, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), $"워드 쓰기 개수는 {MinWordCount}~{MaxWordCount} 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        ushort payloadLen = (ushort)(DescriptorLength + wordData.Length);
        byte[] frame = new byte[HeaderLength + payloadLen];

        FillHeader(frame, connectionId, command: CommandWriteWords, payloadLen);
        FillDescriptor(frame, address, wordCount);
        Buffer.BlockCopy(wordData, 0, frame, ReadFrameLength, wordData.Length);
        return frame;
    }

    private static void FillHeader(byte[] frame, byte connectionId, byte command, ushort payloadLength)
    {
        frame[0] = FrameHeader;
        frame[1] = 0x80;
        frame[2] = 0x80;
        frame[3] = 0x00;
        frame[4] = 0xFF; // 요청 시 0xFF
        frame[5] = FrameType;
        frame[6] = connectionId;
        frame[7] = 0x00;
        frame[8] = 0x11;
        // 9~13 = 0x00
        frame[14] = command;
        frame[15] = 0x00;
        frame[16] = 0x00;
        frame[17] = 0x01; // 1블록
        BinaryBuffer.WriteUInt16LE(frame, LengthFieldOffset, payloadLength);
    }

    private static void FillDescriptor(byte[] frame, FujiElectricSphAddress address, ushort wordCount)
    {
        frame[HeaderLength] = address.AreaCode;
        frame[HeaderLength + 1] = (byte)(address.WordAddress & ByteMask);
        frame[HeaderLength + 2] = (byte)((address.WordAddress >> MidByteShift) & ByteMask);
        frame[HeaderLength + 3] = (byte)((address.WordAddress >> HighByteShift) & ByteMask);
        BinaryBuffer.WriteUInt16LE(frame, HeaderLength + 4, wordCount);
    }
}
