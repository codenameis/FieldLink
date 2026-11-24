using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class VigorRequestBuilder
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;
    private const byte FunctionReadWord = 0x20;
    private const byte FunctionReadBit = 0x21;
    private const byte FunctionWriteWord = 0x28;
    private const byte FunctionWriteBit = 0x29;
    private const int FixedRequestBodyLength = 7;
    private const int BodyPrefixLength = 3;
    private const int PayloadStartOffset = BodyPrefixLength + FixedRequestBodyLength;
    private const int StationOffset = 0;
    private const int BodyLengthOffset = 1;
    private const int FunctionCodeOffset = 3;
    private const int DeviceCodeOffset = 4;
    private const int AddressStartOffset = 5;
    private const int CountOffset = 8;
    private const int BytesPerWord = 2;
    private const int BytesPer32BitWord = 4;
    private const int BitsPerByte = 8;
    private const int FrameWrapperLength = 6;
    private const int HighByteShift = 8;
    private const int ByteMask = 0xFF;

    /// <summary>연속 워드/비트 읽기 요청 프레임을 생성합니다. (기능 코드 0x20/0x21)</summary>
    public static byte[] BuildReadRequest(byte station, VigorAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "읽기 개수는 1 이상이어야 합니다.");
        }

        byte functionCode = address.IsBit ? FunctionReadBit : FunctionReadWord;
        byte[] body = new byte[PayloadStartOffset];
        FillBodyHeader(body, station, FixedRequestBodyLength, functionCode, address, count);
        return WrapFrame(body);
    }

    /// <summary>연속 워드 쓰기 요청 프레임을 생성합니다. (기능 코드 0x28)</summary>
    public static byte[] BuildWriteWordRequest(byte station, VigorAddress address, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException($"워드 영역 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("쓸 워드 데이터가 비어 있습니다.", nameof(data));
        }

        int unitBytes = address.Is32BitWord ? BytesPer32BitWord : BytesPerWord;
        if (data.Length % unitBytes != 0)
        {
            throw new ArgumentException($"데이터 길이({data.Length})는 디바이스 단위 바이트({unitBytes})의 배수여야 합니다.", nameof(data));
        }

        ushort wordCount = checked((ushort)(data.Length / unitBytes));
        ushort bodyLen = checked((ushort)(FixedRequestBodyLength + data.Length));

        byte[] body = new byte[BodyPrefixLength + bodyLen];
        FillBodyHeader(body, station, bodyLen, FunctionWriteWord, address, wordCount);
        Buffer.BlockCopy(data, 0, body, PayloadStartOffset, data.Length);

        return WrapFrame(body);
    }

    /// <summary>연속 비트 쓰기 요청 프레임을 생성합니다. (기능 코드 0x29)</summary>
    public static byte[] BuildWriteBitRequest(byte station, VigorAddress address, bool[] values)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException($"비트 접점 주소가 필요합니다: {address.Original}", nameof(address));
        }

        if (values is null || values.Length == 0 || values.Length > ushort.MaxValue)
        {
            throw new ArgumentException("쓸 비트 데이터가 비어 있거나 최대 길이를 초과했습니다.", nameof(values));
        }

        int byteCount = (values.Length + BitsPerByte - 1) / BitsPerByte;
        ushort bodyLen = checked((ushort)(FixedRequestBodyLength + byteCount));
        byte[] body = new byte[BodyPrefixLength + bodyLen];
        FillBodyHeader(body, station, bodyLen, FunctionWriteBit, address, (ushort)values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                body[PayloadStartOffset + (i / BitsPerByte)] |= (byte)(1 << (i % BitsPerByte));
            }
        }

        return WrapFrame(body);
    }

    private static void FillBodyHeader(
        byte[] body,
        byte station,
        ushort bodyLen,
        byte functionCode,
        VigorAddress address,
        ushort count)
    {
        body[StationOffset] = station;
        BinaryBuffer.WriteUInt16LE(body, BodyLengthOffset, bodyLen);
        body[FunctionCodeOffset] = functionCode;
        body[DeviceCodeOffset] = address.DeviceCode;
        body[AddressStartOffset] = address.BcdAddress[0];
        body[AddressStartOffset + 1] = address.BcdAddress[1];
        body[AddressStartOffset + 2] = address.BcdAddress[2];
        BinaryBuffer.WriteUInt16LE(body, CountOffset, count);
    }

    private static byte[] WrapFrame(byte[] body)
    {
        byte sum = 0;
        int dleCount = 0;
        for (int i = 0; i < body.Length; i++)
        {
            byte b = body[i];
            sum = (byte)(sum + b);
            if (b == Dle)
            {
                dleCount++;
            }
        }

        // DLE(1) + STX(1) + EscapedBody(body.Length + dleCount) + DLE(1) + ETX(1) + ChecksumHex(2)
        byte[] frame = new byte[FrameWrapperLength + body.Length + dleCount];
        int offset = 0;
        frame[offset++] = Dle;
        frame[offset++] = Stx;

        for (int i = 0; i < body.Length; i++)
        {
            byte b = body[i];
            frame[offset++] = b;
            if (b == Dle)
            {
                frame[offset++] = Dle;
            }
        }

        frame[offset++] = Dle;
        frame[offset++] = Etx;
        BinaryBuffer.WriteAsciiHexByte(frame, offset, (byte)((sum >> HighByteShift) != 0 ? (sum & ByteMask) : sum));

        return frame;
    }
}
