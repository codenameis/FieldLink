using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc SRTP 요청 프레임을 생성하는 빌더입니다. (사양서 §1)</summary>
public static class GESrtpRequestBuilder
{
    /// <summary>세션 초기화용 56바이트 제로 프레임을 생성합니다. (사양서 §1.1)</summary>
    public static byte[] BuildInitSession() => new byte[56];

    /// <summary>시스템 메모리 읽기(서비스 코드 0x04) 프레임을 생성합니다. (사양서 §1.3)</summary>
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

        byte[] frame = new byte[56];
        frame[0] = 0x02; // 서비스 요청
        BinaryBuffer.WriteUInt16LE(frame, 2, sequenceId);
        frame[4] = 0x00; // 확장 페이로드 길이 0
        frame[5] = 0x00;
        frame[9] = 0x01; // 단일 헤더 전송
        frame[17] = 0x01;
        frame[30] = 0x06;
        frame[31] = 0xC0; // 단문 요청
        frame[36] = 0x10;
        frame[37] = 0x0E;
        frame[40] = 0x01;
        frame[41] = 0x01;

        frame[42] = 0x04; // Read System Memory
        frame[43] = address.MemoryTypeCode;
        BinaryBuffer.WriteUInt16LE(frame, 44, address.WireAddress);
        BinaryBuffer.WriteUInt16LE(frame, 46, count);

        return frame;
    }

    /// <summary>시스템 메모리 쓰기(서비스 코드 0x07) 프레임을 생성합니다. (사양서 §1.3)</summary>
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

        ushort payloadLen = (ushort)data.Length;
        byte[] frame = new byte[56 + payloadLen];

        frame[0] = 0x02; // 서비스 요청
        BinaryBuffer.WriteUInt16LE(frame, 2, sequenceId);
        BinaryBuffer.WriteUInt16LE(frame, 4, payloadLen);
        frame[9] = 0x02; // 확장 페이로드 동반
        frame[17] = 0x02;
        frame[30] = 0x09;
        frame[31] = 0x80; // 확장 요청
        frame[36] = 0x10;
        frame[37] = 0x0E;
        frame[40] = 0x01;
        frame[41] = 0x01;

        frame[42] = 0x02; // 확장 블록 지시자
        frame[48] = 0x01;
        frame[49] = 0x01;
        frame[50] = 0x07; // Write System Memory
        frame[51] = address.MemoryTypeCode;
        BinaryBuffer.WriteUInt16LE(frame, 52, address.WireAddress);
        BinaryBuffer.WriteUInt16LE(frame, 54, count);

        Buffer.BlockCopy(data, 0, frame, 56, data.Length);
        return frame;
    }
}
