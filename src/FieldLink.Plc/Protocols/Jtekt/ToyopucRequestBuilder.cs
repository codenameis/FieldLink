using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class ToyopucRequestBuilder
{
    private const int HeaderLength = 4;
    private const int LengthFieldOffset = 2;
    private const int CommandOffset = 4;
    private const int BytesPerWord = 2;
    private const ushort BasicReadWordsBodyLength = 5;
    private const ushort ExtendedReadWordsBodyLength = 6;
    private const int BasicWriteWordsHeaderLength = 7;
    private const int ExtendedWriteWordsHeaderLength = 8;
    private const byte BasicReadWordsCommand = 0x1C;
    private const byte BasicWriteWordsCommand = 0x1D;
    private const byte ReadBitCommand = 0x20;
    private const byte WriteBitCommand = 0x21;
    private const byte ExtendedReadWordsCommand = 0x94;
    private const byte ExtendedWriteWordsCommand = 0x95;
    private const byte ReadBitBodyLength = 0x03;
    private const byte WriteBitBodyLength = 0x04;

    /// <summary>단일 비트 읽기(명령 0x20) 요청 프레임을 조립합니다.</summary>
    /// <param name="address">대상 비트 주소입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadBit(ToyopucAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] frame = new byte[HeaderLength + ReadBitBodyLength];
        frame[LengthFieldOffset] = ReadBitBodyLength;
        frame[CommandOffset] = ReadBitCommand;
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

        byte[] frame = new byte[HeaderLength + WriteBitBodyLength];
        frame[LengthFieldOffset] = WriteBitBodyLength;
        frame[CommandOffset] = WriteBitCommand;
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
            byte[] frame = new byte[HeaderLength + ExtendedReadWordsBodyLength];
            frame[LengthFieldOffset] = (byte)ExtendedReadWordsBodyLength;
            frame[CommandOffset] = ExtendedReadWordsCommand;
            frame[5] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LE(frame, 6, address.WireAddress);
            BinaryBuffer.WriteUInt16LE(frame, 8, wordCount);
            return frame;
        }
        else
        {
            byte[] frame = new byte[HeaderLength + BasicReadWordsBodyLength];
            frame[LengthFieldOffset] = (byte)BasicReadWordsBodyLength;
            frame[CommandOffset] = BasicReadWordsCommand;
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

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        if (address.Prg.HasValue)
        {
            ushort bodyLen = (ushort)(HeaderLength + wordData.Length);
            byte[] frame = new byte[HeaderLength + bodyLen];
            BinaryBuffer.WriteUInt16LE(frame, LengthFieldOffset, bodyLen);
            frame[CommandOffset] = ExtendedWriteWordsCommand;
            frame[5] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LE(frame, 6, address.WireAddress);
            Buffer.BlockCopy(wordData, 0, frame, ExtendedWriteWordsHeaderLength, wordData.Length);
            return frame;
        }
        else
        {
            ushort bodyLen = (ushort)(ReadBitBodyLength + wordData.Length);
            byte[] frame = new byte[HeaderLength + bodyLen];
            BinaryBuffer.WriteUInt16LE(frame, LengthFieldOffset, bodyLen);
            frame[CommandOffset] = BasicWriteWordsCommand;
            BinaryBuffer.WriteUInt16LE(frame, 5, address.WireAddress);
            Buffer.BlockCopy(wordData, 0, frame, BasicWriteWordsHeaderLength, wordData.Length);
            return frame;
        }
    }
}
