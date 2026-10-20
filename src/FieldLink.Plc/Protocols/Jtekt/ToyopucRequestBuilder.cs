using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class ToyopucRequestBuilder
{
    /// <summary>단일 비트 읽기(명령 0x20) 요청 프레임을 조립합니다.</summary>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadBit(ToyopucAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] frame = new byte[7];
        frame[2] = 0x03; // Body length = 3
        frame[4] = 0x20; // CMD
        BinaryBuffer.WriteUInt16LE(frame, 5, address.WireAddress);
        return frame;
    }

    /// <summary>단일 비트 쓰기(명령 0x21) 요청 프레임을 조립합니다.</summary>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <param name="bitValue">기록할 비트 값입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteBit(ToyopucAddress address, bool bitValue)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] frame = new byte[8];
        frame[2] = 0x04; // Body length = 4
        frame[4] = 0x21; // CMD
        BinaryBuffer.WriteUInt16LE(frame, 5, address.WireAddress);
        frame[7] = (byte)(bitValue ? 0x01 : 0x00);
        return frame;
    }

    /// <summary>연속 워드 읽기(기본 0x1C / 확장 0x94) 요청 프레임을 조립합니다.</summary>
    /// <param name="address">대상 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 수입니다. 1 이상.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadWords(ToyopucAddress address, ushort wordCount)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 개수는 1 이상이어야 합니다.");
        }

        if (address.Prg.HasValue)
        {
            byte[] frame = new byte[10];
            frame[2] = 0x06; // Body length = 6
            frame[4] = 0x94; // Extended CMD
            frame[5] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LE(frame, 6, address.WireAddress);
            BinaryBuffer.WriteUInt16LE(frame, 8, wordCount);
            return frame;
        }
        else
        {
            byte[] frame = new byte[9];
            frame[2] = 0x05; // Body length = 5
            frame[4] = 0x1C; // Basic CMD
            BinaryBuffer.WriteUInt16LE(frame, 5, address.WireAddress);
            BinaryBuffer.WriteUInt16LE(frame, 7, wordCount);
            return frame;
        }
    }

    /// <summary>연속 워드 쓰기(기본 0x1D / 확장 0x95) 요청 프레임을 조립합니다.</summary>
    /// <param name="address">대상 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 수입니다. 1 이상.</param>
    /// <param name="wordData">기록할 워드 데이터 바이트 배열(리틀 엔디언)입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteWords(ToyopucAddress address, ushort wordCount, byte[] wordData)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (wordCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 개수는 1 이상이어야 합니다.");
        }

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
            ushort bodyLen = (ushort)(4 + wordData.Length);
            byte[] frame = new byte[4 + bodyLen];
            BinaryBuffer.WriteUInt16LE(frame, 2, bodyLen);
            frame[4] = 0x95;
            frame[5] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LE(frame, 6, address.WireAddress);
            Buffer.BlockCopy(wordData, 0, frame, 8, wordData.Length);
            return frame;
        }
        else
        {
            ushort bodyLen = (ushort)(3 + wordData.Length);
            byte[] frame = new byte[4 + bodyLen];
            BinaryBuffer.WriteUInt16LE(frame, 2, bodyLen);
            frame[4] = 0x1D;
            BinaryBuffer.WriteUInt16LE(frame, 5, address.WireAddress);
            Buffer.BlockCopy(wordData, 0, frame, 7, wordData.Length);
            return frame;
        }
    }
}
