using System;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 요청 프레임을 생성하는 빌더입니다. (사양서 Part C §C.1.1)</summary>
public static class FujiElectricSphRequestBuilder
{
    private const byte FrameHeader = 0xFB;
    private const byte FrameType = 0x7B;

    /// <summary>연속 워드 읽기 요청 프레임을 조립합니다.</summary>
    public static byte[] BuildReadWords(byte connectionId, FujiElectricSphAddress address, ushort wordCount)
    {
        if (wordCount is < 1 or > 230)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~230 사이여야 합니다.");
        }

        byte[] frame = new byte[26];
        FillHeader(frame, connectionId, command: 0x00, payloadLength: 6);
        FillDescriptor(frame, address, wordCount);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 조립합니다.</summary>
    public static byte[] BuildWriteWords(byte connectionId, FujiElectricSphAddress address, ushort wordCount, byte[] wordData)
    {
        if (wordCount is < 1 or > 230)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~230 사이여야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * 2)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        ushort payloadLen = (ushort)(6 + wordData.Length);
        byte[] frame = new byte[20 + payloadLen];

        FillHeader(frame, connectionId, command: 0x01, payloadLen);
        FillDescriptor(frame, address, wordCount);
        Array.Copy(wordData, 0, frame, 26, wordData.Length);
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
        frame[18] = (byte)(payloadLength & 0xFF);
        frame[19] = (byte)((payloadLength >> 8) & 0xFF);
    }

    private static void FillDescriptor(byte[] frame, FujiElectricSphAddress address, ushort wordCount)
    {
        frame[20] = address.AreaCode;
        frame[21] = (byte)(address.WordAddress & 0xFF);
        frame[22] = (byte)((address.WordAddress >> 8) & 0xFF);
        frame[23] = (byte)((address.WordAddress >> 16) & 0xFF);
        frame[24] = (byte)(wordCount & 0xFF);
        frame[25] = (byte)((wordCount >> 8) & 0xFF);
    }
}
