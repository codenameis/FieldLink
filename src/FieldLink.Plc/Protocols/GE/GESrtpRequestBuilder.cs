using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 요청 프레임을 생성하는 정적 빌더입니다. (사양서 §1)</summary>
public static class GESrtpRequestBuilder
{
    private const int HeaderLength = 56;

    private const int PacketTypeOffset = 0;
    private const int SequenceIdOffset = 2;
    private const int ExtendedPayloadLengthOffset = 4;
    private const int TransferSubtypeOffset = 9;
    private const int BlockTypeOffset = 17;
    private const int MailboxSequenceOffset = 30;
    private const int MessageTypeOffset = 31;
    private const int ProtocolMarkerLowOffset = 36;
    private const int ProtocolMarkerHighOffset = 37;
    private const int ServiceBlockHeaderFirstOffset = 40;
    private const int ServiceBlockHeaderSecondOffset = 41;

    private const int ReadServiceCodeOffset = 42;
    private const int ReadMemoryTypeOffset = 43;
    private const int ReadStartAddressOffset = 44;
    private const int ReadQuantityOffset = 46;

    private const int WriteExtendedBlockIndicatorOffset = 42;
    private const int WriteExtendedHeaderFirstOffset = 48;
    private const int WriteExtendedHeaderSecondOffset = 49;
    private const int WriteServiceCodeOffset = 50;
    private const int WriteMemoryTypeOffset = 51;
    private const int WriteStartAddressOffset = 52;
    private const int WriteQuantityOffset = 54;

    private const byte ServiceRequestPacketType = 0x02;
    private const byte SingleHeaderTransferSubtype = 0x01;
    private const byte ExtendedPayloadTransferSubtype = 0x02;
    private const byte SingleBlockType = 0x01;
    private const byte ExtendedBlockType = 0x02;
    private const byte ReadMailboxSequence = 0x06;
    private const byte WriteMailboxSequence = 0x09;
    private const byte ShortRequestMessageType = 0xC0;
    private const byte ExtendedRequestMessageType = 0x80;
    private const byte ProtocolMarkerLow = 0x10;
    private const byte ProtocolMarkerHigh = 0x0E;
    private const byte ServiceBlockMarker = 0x01;
    private const byte ExtendedServiceBlockIndicator = 0x02;
    private const byte ReadSystemMemoryServiceCode = 0x04;
    private const byte WriteSystemMemoryServiceCode = 0x07;

    /// <summary>세션 초기화용 56바이트 제로 프레임을 생성합니다. (사양서 §1.1)</summary>
    /// <returns>전체 바이트가 0x00인 56바이트 배열입니다.</returns>
    public static byte[] BuildInitSession() => new byte[HeaderLength];

    /// <summary>시스템 메모리 읽기(서비스 코드 0x04) 요청 프레임을 생성합니다. (사양서 §1.3)</summary>
    /// <param name="sequenceId">요청 순번 식별자입니다.</param>
    /// <param name="address">읽을 메모리 영역과 시작 주소입니다.</param>
    /// <param name="count">읽을 단위 개수(워드, 바이트 또는 비트 수)입니다.</param>
    /// <returns>생성된 56바이트 SRTP 읽기 요청 프레임입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/>가 0일 때.</exception>
    public static byte[] BuildReadRequest(ushort sequenceId, GESrtpAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "읽기 개수는 1 이상이어야 합니다.");
        }

        byte[] frame = new byte[HeaderLength];
        frame[PacketTypeOffset] = ServiceRequestPacketType;
        BinaryBuffer.WriteUInt16LittleEndian(frame, SequenceIdOffset, sequenceId);
        frame[TransferSubtypeOffset] = SingleHeaderTransferSubtype;
        frame[BlockTypeOffset] = SingleBlockType;
        frame[MailboxSequenceOffset] = ReadMailboxSequence;
        frame[MessageTypeOffset] = ShortRequestMessageType;
        frame[ProtocolMarkerLowOffset] = ProtocolMarkerLow;
        frame[ProtocolMarkerHighOffset] = ProtocolMarkerHigh;
        frame[ServiceBlockHeaderFirstOffset] = ServiceBlockMarker;
        frame[ServiceBlockHeaderSecondOffset] = ServiceBlockMarker;

        frame[ReadServiceCodeOffset] = ReadSystemMemoryServiceCode;
        frame[ReadMemoryTypeOffset] = address.MemoryTypeCode;
        BinaryBuffer.WriteUInt16LittleEndian(frame, ReadStartAddressOffset, address.WireAddress);
        BinaryBuffer.WriteUInt16LittleEndian(frame, ReadQuantityOffset, count);

        return frame;
    }

    /// <summary>시스템 메모리 쓰기(서비스 코드 0x07) 요청 프레임을 생성합니다. (사양서 §1.3)</summary>
    /// <param name="sequenceId">요청 순번 식별자입니다.</param>
    /// <param name="address">쓸 메모리 영역과 시작 주소입니다.</param>
    /// <param name="count">쓸 단위 개수(워드, 바이트 또는 비트 수)입니다.</param>
    /// <param name="data">오프셋 56 이후에 붙일 확장 페이로드 바이트 배열입니다.</param>
    /// <returns>생성된 SRTP 쓰기 요청 프레임입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> 또는 <paramref name="data"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/>가 0일 때.</exception>
    public static byte[] BuildWriteRequest(ushort sequenceId, GESrtpAddress address, ushort count, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "쓰기 개수는 1 이상이어야 합니다.");
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ushort payloadByteCount = (ushort)data.Length;
        byte[] frame = new byte[HeaderLength + payloadByteCount];

        frame[PacketTypeOffset] = ServiceRequestPacketType;
        BinaryBuffer.WriteUInt16LittleEndian(frame, SequenceIdOffset, sequenceId);
        BinaryBuffer.WriteUInt16LittleEndian(frame, ExtendedPayloadLengthOffset, payloadByteCount);
        frame[TransferSubtypeOffset] = ExtendedPayloadTransferSubtype;
        frame[BlockTypeOffset] = ExtendedBlockType;
        frame[MailboxSequenceOffset] = WriteMailboxSequence;
        frame[MessageTypeOffset] = ExtendedRequestMessageType;
        frame[ProtocolMarkerLowOffset] = ProtocolMarkerLow;
        frame[ProtocolMarkerHighOffset] = ProtocolMarkerHigh;
        frame[ServiceBlockHeaderFirstOffset] = ServiceBlockMarker;
        frame[ServiceBlockHeaderSecondOffset] = ServiceBlockMarker;

        frame[WriteExtendedBlockIndicatorOffset] = ExtendedServiceBlockIndicator;
        frame[WriteExtendedHeaderFirstOffset] = ServiceBlockMarker;
        frame[WriteExtendedHeaderSecondOffset] = ServiceBlockMarker;
        frame[WriteServiceCodeOffset] = WriteSystemMemoryServiceCode;
        frame[WriteMemoryTypeOffset] = address.MemoryTypeCode;
        BinaryBuffer.WriteUInt16LittleEndian(frame, WriteStartAddressOffset, address.WireAddress);
        BinaryBuffer.WriteUInt16LittleEndian(frame, WriteQuantityOffset, count);

        Buffer.BlockCopy(data, 0, frame, HeaderLength, data.Length);
        return frame;
    }
}
