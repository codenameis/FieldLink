using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>TOYOPUC Computer Link 2PORT-EFR 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class ToyopucRequestBuilder
{
    private const int HeaderLength = 4;
    private const int LengthFieldOffset = 2;
    private const int CommandOffset = 4;
    private const int BasicWireAddressOffset = 5;
    private const int BasicWordCountOffset = 7;
    private const int BasicWriteDataOffset = 7;
    private const int BasicBitValueOffset = 7;
    private const int ExtendedProgramNumberOffset = 5;
    private const int ExtendedWireAddressOffset = 6;
    private const int ExtendedWordCountOffset = 8;
    private const int ExtendedWriteDataOffset = 8;
    private const int ExtendedBankSize = 0x8000;

    private const ushort ReadBitBodyLength = 3;
    private const ushort WriteBitBodyLength = 4;
    private const ushort BasicReadWordsBodyLength = 5;
    private const ushort ExtendedReadWordsBodyLength = 6;
    private const int BasicWriteWordsHeaderBodySize = 3;
    private const int ExtendedWriteWordsHeaderBodySize = 4;

    private const byte ReadBitCommand = 0x20;
    private const byte WriteBitCommand = 0x21;
    private const byte BasicReadWordsCommand = 0x1C;
    private const byte BasicWriteWordsCommand = 0x1D;
    private const byte ExtendedReadWordsCommand = 0x94;
    private const byte ExtendedWriteWordsCommand = 0x95;

    private const byte BitOnValue = 0x01;
    private const byte BitOffValue = 0x00;
    private const int BytesPerWord = 2;

    /// <summary>단일 비트 읽기(명령 0x20) 요청 프레임을 조립합니다. (사양서 §1.7)</summary>
    /// <param name="address">대상 기본 영역 비트 주소입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildReadBit(ToyopucAddress address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit || address.Prg.HasValue)
        {
            throw new ArgumentException($"기본 영역 비트 주소가 필요합니다: {address.Original}", nameof(address));
        }

        byte[] requestFrame = new byte[HeaderLength + ReadBitBodyLength];
        BinaryBuffer.WriteUInt16LittleEndian(requestFrame, LengthFieldOffset, ReadBitBodyLength);
        requestFrame[CommandOffset] = ReadBitCommand;
        BinaryBuffer.WriteUInt16LittleEndian(requestFrame, BasicWireAddressOffset, address.WireAddress);
        return requestFrame;
    }

    /// <summary>단일 비트 쓰기(명령 0x21) 요청 프레임을 조립합니다. (사양서 §1.7)</summary>
    /// <param name="address">대상 기본 영역 비트 주소입니다.</param>
    /// <param name="bitValue">기록할 비트 값(true: ON, false: OFF)입니다.</param>
    /// <returns>생성된 요청 프레임 바이트 배열입니다.</returns>
    public static byte[] BuildWriteBit(ToyopucAddress address, bool bitValue)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit || address.Prg.HasValue)
        {
            throw new ArgumentException($"기본 영역 비트 주소가 필요합니다: {address.Original}", nameof(address));
        }

        byte[] requestFrame = new byte[HeaderLength + WriteBitBodyLength];
        BinaryBuffer.WriteUInt16LittleEndian(requestFrame, LengthFieldOffset, WriteBitBodyLength);
        requestFrame[CommandOffset] = WriteBitCommand;
        BinaryBuffer.WriteUInt16LittleEndian(requestFrame, BasicWireAddressOffset, address.WireAddress);
        requestFrame[BasicBitValueOffset] = bitValue ? BitOnValue : BitOffValue;
        return requestFrame;
    }

    /// <summary>연속 워드 읽기(기본 0x1C / 확장 0x94) 요청 프레임을 조립합니다. (사양서 §1.7)</summary>
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
            throw new ArgumentOutOfRangeException(nameof(wordCount), wordCount, "워드 개수는 1 이상이어야 합니다.");
        }

        ValidateWordWireRange(address, wordCount);

        if (address.Prg.HasValue)
        {
            byte[] extendedFrame = new byte[HeaderLength + ExtendedReadWordsBodyLength];
            BinaryBuffer.WriteUInt16LittleEndian(extendedFrame, LengthFieldOffset, ExtendedReadWordsBodyLength);
            extendedFrame[CommandOffset] = ExtendedReadWordsCommand;
            extendedFrame[ExtendedProgramNumberOffset] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LittleEndian(extendedFrame, ExtendedWireAddressOffset, address.WireAddress);
            BinaryBuffer.WriteUInt16LittleEndian(extendedFrame, ExtendedWordCountOffset, wordCount);
            return extendedFrame;
        }

        byte[] basicFrame = new byte[HeaderLength + BasicReadWordsBodyLength];
        BinaryBuffer.WriteUInt16LittleEndian(basicFrame, LengthFieldOffset, BasicReadWordsBodyLength);
        basicFrame[CommandOffset] = BasicReadWordsCommand;
        BinaryBuffer.WriteUInt16LittleEndian(basicFrame, BasicWireAddressOffset, address.WireAddress);
        BinaryBuffer.WriteUInt16LittleEndian(basicFrame, BasicWordCountOffset, wordCount);
        return basicFrame;
    }

    /// <summary>연속 워드 쓰기(기본 0x1D / 확장 0x95) 요청 프레임을 조립합니다. (사양서 §1.7)</summary>
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
            throw new ArgumentOutOfRangeException(nameof(wordCount), wordCount, "워드 개수는 1 이상이어야 합니다.");
        }

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        ValidateWordWireRange(address, wordCount);

        int headerBodySize = address.Prg.HasValue ? ExtendedWriteWordsHeaderBodySize : BasicWriteWordsHeaderBodySize;
        int bodyLength = headerBodySize + wordData.Length;
        if (bodyLength > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"본문 길이({bodyLength})가 16비트 프레임 길이 상한({ushort.MaxValue})을 초과합니다.");
        }

        ushort wireBodyLength = (ushort)bodyLength;
        if (address.Prg.HasValue)
        {
            byte[] extendedFrame = new byte[HeaderLength + wireBodyLength];
            BinaryBuffer.WriteUInt16LittleEndian(extendedFrame, LengthFieldOffset, wireBodyLength);
            extendedFrame[CommandOffset] = ExtendedWriteWordsCommand;
            extendedFrame[ExtendedProgramNumberOffset] = address.Prg.Value;
            BinaryBuffer.WriteUInt16LittleEndian(extendedFrame, ExtendedWireAddressOffset, address.WireAddress);
            Buffer.BlockCopy(wordData, 0, extendedFrame, ExtendedWriteDataOffset, wordData.Length);
            return extendedFrame;
        }

        byte[] basicFrame = new byte[HeaderLength + wireBodyLength];
        BinaryBuffer.WriteUInt16LittleEndian(basicFrame, LengthFieldOffset, wireBodyLength);
        basicFrame[CommandOffset] = BasicWriteWordsCommand;
        BinaryBuffer.WriteUInt16LittleEndian(basicFrame, BasicWireAddressOffset, address.WireAddress);
        Buffer.BlockCopy(wordData, 0, basicFrame, BasicWriteDataOffset, wordData.Length);
        return basicFrame;
    }

    private static void ValidateWordWireRange(ToyopucAddress address, ushort wordCount)
    {
        if (address.Device == "EB" && (long)address.WireAddress + wordCount > ExtendedBankSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"EB 단일 요청이 0x{ExtendedBankSize:X} 뱅크 경계를 초과합니다.");
        }

        if ((long)address.WireAddress + wordCount - 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                "TOYOPUC 요청 워드 범위가 16비트 와이어 주소 상한을 초과합니다.");
        }
    }
}
