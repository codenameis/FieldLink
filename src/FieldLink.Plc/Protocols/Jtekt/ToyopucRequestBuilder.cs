using System;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class ToyopucRequestBuilder
{
    /// <summary>기본 영역 단일 비트 읽기(명령 0x20) 프레임을 생성합니다. (사양서 §1.7)</summary>
    public static byte[] BuildReadBit(ToyopucAddress address)
    {
        byte[] frame = new byte[7];
        frame[0] = 0x00; // Request
        frame[1] = 0x00;
        frame[2] = 0x03; // Body length = 3
        frame[3] = 0x00;
        frame[4] = 0x20; // CMD
        frame[5] = (byte)(address.WireAddress & 0xFF);
        frame[6] = (byte)((address.WireAddress >> 8) & 0xFF);
        return frame;
    }

    /// <summary>기본 영역 단일 비트 쓰기(명령 0x21) 프레임을 생성합니다. (사양서 §1.7)</summary>
    public static byte[] BuildWriteBit(ToyopucAddress address, bool bitValue)
    {
        byte[] frame = new byte[8];
        frame[0] = 0x00;
        frame[1] = 0x00;
        frame[2] = 0x04; // Body length = 4
        frame[3] = 0x00;
        frame[4] = 0x21; // CMD
        frame[5] = (byte)(address.WireAddress & 0xFF);
        frame[6] = (byte)((address.WireAddress >> 8) & 0xFF);
        frame[7] = (byte)(bitValue ? 0x01 : 0x00);
        return frame;
    }

    /// <summary>연속 워드 읽기(기본 0x1C 또는 확장 0x94) 프레임을 생성합니다. (사양서 §1.7)</summary>
    public static byte[] BuildReadWords(ToyopucAddress address, ushort wordCount)
    {
        if (address.Prg.HasValue)
        {
            // 확장 영역 (CMD 0x94)
            byte[] frame = new byte[10];
            frame[0] = 0x00;
            frame[1] = 0x00;
            frame[2] = 0x06; // Body length = 6
            frame[3] = 0x00;
            frame[4] = 0x94; // CMD
            frame[5] = address.Prg.Value;
            frame[6] = (byte)(address.WireAddress & 0xFF);
            frame[7] = (byte)((address.WireAddress >> 8) & 0xFF);
            frame[8] = (byte)(wordCount & 0xFF);
            frame[9] = (byte)((wordCount >> 8) & 0xFF);
            return frame;
        }
        else
        {
            // 기본 영역 (CMD 0x1C)
            byte[] frame = new byte[9];
            frame[0] = 0x00;
            frame[1] = 0x00;
            frame[2] = 0x05; // Body length = 5
            frame[3] = 0x00;
            frame[4] = 0x1C; // CMD
            frame[5] = (byte)(address.WireAddress & 0xFF);
            frame[6] = (byte)((address.WireAddress >> 8) & 0xFF);
            frame[7] = (byte)(wordCount & 0xFF);
            frame[8] = (byte)((wordCount >> 8) & 0xFF);
            return frame;
        }
    }

    /// <summary>연속 워드 쓰기(기본 0x1D 또는 확장 0x95) 프레임을 생성합니다. (사양서 §1.7)</summary>
    public static byte[] BuildWriteWords(ToyopucAddress address, ushort wordCount, byte[] wordData)
    {
        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * 2)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        if (address.Prg.HasValue)
        {
            // 확장 영역 (CMD 0x95)
            ushort bodyLen = (ushort)(4 + wordData.Length);
            byte[] frame = new byte[4 + bodyLen];
            frame[0] = 0x00;
            frame[1] = 0x00;
            frame[2] = (byte)(bodyLen & 0xFF);
            frame[3] = (byte)((bodyLen >> 8) & 0xFF);
            frame[4] = 0x95;
            frame[5] = address.Prg.Value;
            frame[6] = (byte)(address.WireAddress & 0xFF);
            frame[7] = (byte)((address.WireAddress >> 8) & 0xFF);
            Array.Copy(wordData, 0, frame, 8, wordData.Length);
            return frame;
        }
        else
        {
            // 기본 영역 (CMD 0x1D)
            ushort bodyLen = (ushort)(3 + wordData.Length);
            byte[] frame = new byte[4 + bodyLen];
            frame[0] = 0x00;
            frame[1] = 0x00;
            frame[2] = (byte)(bodyLen & 0xFF);
            frame[3] = (byte)((bodyLen >> 8) & 0xFF);
            frame[4] = 0x1D;
            frame[5] = (byte)(address.WireAddress & 0xFF);
            frame[6] = (byte)((address.WireAddress >> 8) & 0xFF);
            Array.Copy(wordData, 0, frame, 7, wordData.Length);
            return frame;
        }
    }
}
