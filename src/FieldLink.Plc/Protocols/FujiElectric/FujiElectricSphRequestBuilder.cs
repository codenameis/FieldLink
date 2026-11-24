using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 요청 프레임을 생성하는 빌더입니다. (사양서 Part C §C.1.1)</summary>
public static class FujiElectricSphRequestBuilder
{
    private const byte FrameStartIdentifier = 0xFB;
    private const byte ProtocolVersionControl = 0x80;
    private const byte SingleFrameType = 0x80;
    private const byte ReservedZero = 0x00;
    private const byte RequestStatusByte = 0xFF;
    private const byte LoaderCommandFrameType = 0x7B;
    private const byte DefaultCpuRoutingByte = 0x11;
    private const byte ReadCommandCode = 0x00;
    private const byte WriteCommandCode = 0x01;
    private const byte ContinuousWordSubmode = 0x00;
    private const byte SingleBlockCount = 0x01;

    private const int HeaderByteLength = 20;
    private const ushort DescriptorByteLength = 6;
    private const int ReadRequestFrameLength = HeaderByteLength + DescriptorByteLength;
    private const int VersionControlOffset = 1;
    private const int SingleFrameTypeOffset = 2;
    private const int ReservedHeaderOffset = 3;
    private const int StatusOffset = 4;
    private const int FrameTypeOffset = 5;
    private const int ConnectionIdOffset = 6;
    private const int RoutingHighOffset = 7;
    private const int RoutingLowOffset = 8;
    private const int CommandOffset = 14;
    private const int SubmodeOffset = 15;
    private const int ReservedCommandOffset = 16;
    private const int BlockCountOffset = 17;
    private const int PayloadLengthOffset = 18;
    private const int AreaCodeOffset = 20;
    private const int WordAddressLowOffset = 21;
    private const int WordAddressMiddleOffset = 22;
    private const int WordAddressHighOffset = 23;
    private const int WordCountOffset = 24;
    private const int WordDataOffset = 26;

    private const byte MinConnectionId = 0x01;
    private const byte MaxConnectionId = 0xFE;
    private const ushort MinWordCount = 1;
    private const ushort MaxWordCount = 230;
    private const int BytesPerWord = 2;
    private const int BitsShift8 = 8;
    private const int BitsShift16 = 16;
    private const uint ByteMask = 0xFFu;

    /// <summary>연속 워드 읽기 요청 프레임을 조립합니다.</summary>
    /// <param name="connectionId">SX-Bus 연결 스테이션 ID입니다.</param>
    /// <param name="address">읽기 시작 워드 주소입니다.</param>
    /// <param name="wordCount">읽을 워드 개수(1~230)입니다.</param>
    /// <returns>조립된 26바이트 요청 프레임입니다.</returns>
    public static byte[] BuildReadWords(byte connectionId, FujiElectricSphAddress address, ushort wordCount)
    {
        ValidateConnectionId(connectionId);

        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("SPH 워드 요청에는 비트 주소를 사용할 수 없습니다.", nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 읽기 개수는 1~230 사이여야 합니다.");
        }

        address.ValidateWordRange(wordCount, nameof(wordCount));

        byte[] frame = new byte[ReadRequestFrameLength];
        FillHeader(frame, connectionId, ReadCommandCode, DescriptorByteLength);
        FillDescriptor(frame, address, wordCount);
        return frame;
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 조립합니다.</summary>
    /// <param name="connectionId">SX-Bus 연결 스테이션 ID입니다.</param>
    /// <param name="address">쓰기 시작 워드 주소입니다.</param>
    /// <param name="wordCount">쓸 워드 개수(1~230)입니다.</param>
    /// <param name="wordData">리틀엔디언 워드 데이터 바이트 배열입니다.</param>
    /// <returns>조립된 쓰기 요청 프레임입니다.</returns>
    public static byte[] BuildWriteWords(
        byte connectionId,
        FujiElectricSphAddress address,
        ushort wordCount,
        byte[] wordData)
    {
        ValidateConnectionId(connectionId);

        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("SPH 워드 요청에는 비트 주소를 사용할 수 없습니다.", nameof(address));
        }

        if (wordCount is < MinWordCount or > MaxWordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(wordCount), "워드 쓰기 개수는 1~230 사이여야 합니다.");
        }

        address.ValidateWordRange(wordCount, nameof(wordCount));

        if (wordData is null)
        {
            throw new ArgumentNullException(nameof(wordData));
        }

        if (wordData.Length != wordCount * BytesPerWord)
        {
            throw new ArgumentException("데이터 길이가 워드 개수와 일치하지 않습니다.", nameof(wordData));
        }

        ushort payloadByteLength = (ushort)(DescriptorByteLength + wordData.Length);
        byte[] frame = new byte[HeaderByteLength + payloadByteLength];

        FillHeader(frame, connectionId, WriteCommandCode, payloadByteLength);
        FillDescriptor(frame, address, wordCount);
        Buffer.BlockCopy(wordData, 0, frame, WordDataOffset, wordData.Length);
        return frame;
    }

    private static void ValidateConnectionId(byte connectionId)
    {
        if (connectionId < MinConnectionId || connectionId > MaxConnectionId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(connectionId),
                connectionId,
                $"연결 ID는 0x{MinConnectionId:X2}~0x{MaxConnectionId:X2} 사이여야 합니다.");
        }
    }

    private static void FillHeader(byte[] frame, byte connectionId, byte commandCode, ushort payloadByteLength)
    {
        frame[0] = FrameStartIdentifier;
        frame[VersionControlOffset] = ProtocolVersionControl;
        frame[SingleFrameTypeOffset] = SingleFrameType;
        frame[ReservedHeaderOffset] = ReservedZero;
        frame[StatusOffset] = RequestStatusByte;
        frame[FrameTypeOffset] = LoaderCommandFrameType;
        frame[ConnectionIdOffset] = connectionId;
        frame[RoutingHighOffset] = ReservedZero;
        frame[RoutingLowOffset] = DefaultCpuRoutingByte;
        frame[CommandOffset] = commandCode;
        frame[SubmodeOffset] = ContinuousWordSubmode;
        frame[ReservedCommandOffset] = ReservedZero;
        frame[BlockCountOffset] = SingleBlockCount;
        BinaryBuffer.WriteUInt16LittleEndian(frame, PayloadLengthOffset, payloadByteLength);
    }

    private static void FillDescriptor(byte[] frame, FujiElectricSphAddress address, ushort wordCount)
    {
        frame[AreaCodeOffset] = address.AreaCode;
        frame[WordAddressLowOffset] = (byte)(address.WordAddress & ByteMask);
        frame[WordAddressMiddleOffset] = (byte)((address.WordAddress >> BitsShift8) & ByteMask);
        frame[WordAddressHighOffset] = (byte)((address.WordAddress >> BitsShift16) & ByteMask);
        BinaryBuffer.WriteUInt16LittleEndian(frame, WordCountOffset, wordCount);
    }
}
