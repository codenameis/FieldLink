using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 요청 프레임 조립기입니다. (사양서 §1.1~§1.4)</summary>
public static class VigorRequestBuilder
{
    private const byte Dle = 0x10;
    private const byte Stx = 0x02;
    private const byte Etx = 0x03;

    private const byte FunctionReadWords = 0x20;
    private const byte FunctionReadBits = 0x21;
    private const byte FunctionWriteWords = 0x28;
    private const byte FunctionWriteBits = 0x29;

    private const ushort ReadBodyCommandLength = 7;
    private const int BodyPrefixLength = 3;
    private const int CommandHeaderLength = 7;
    private const int WritePayloadOffset = BodyPrefixLength + CommandHeaderLength;
    private const int StationOffset = 0;
    private const int BodyLengthOffset = 1;
    private const int FunctionCodeOffset = 3;
    private const int DeviceCodeOffset = 4;
    private const int AddressLowOffset = 5;
    private const int AddressMiddleOffset = 6;
    private const int AddressHighOffset = 7;
    private const int CountOffset = 8;
    private const int BytesPer16BitWord = 2;
    private const int BytesPer32BitCounter = 4;
    private const int BitsPerByte = 8;
    private const int FrameEnvelopeOverhead = 6;

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

        byte functionCode = address.IsBit ? FunctionReadBits : FunctionReadWords;
        byte[] body = new byte[BodyPrefixLength + ReadBodyCommandLength];
        FillBodyHeader(body, station, ReadBodyCommandLength, functionCode, address, count);
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

        int bytesPerDeviceUnit = address.Is32BitWord ? BytesPer32BitCounter : BytesPer16BitWord;
        if (data.Length % bytesPerDeviceUnit != 0)
        {
            throw new ArgumentException(
                $"쓸 워드 데이터 길이({data.Length}바이트)가 디바이스 단위 크기({bytesPerDeviceUnit}바이트)의 배수가 아닙니다.",
                nameof(data));
        }

        ushort wordCount = checked((ushort)(data.Length / bytesPerDeviceUnit));
        ushort bodyLength = checked((ushort)(CommandHeaderLength + data.Length));

        byte[] body = new byte[BodyPrefixLength + bodyLength];
        FillBodyHeader(body, station, bodyLength, FunctionWriteWords, address, wordCount);
        Buffer.BlockCopy(data, 0, body, WritePayloadOffset, data.Length);

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

        int byteCount = (values.Length + (BitsPerByte - 1)) / BitsPerByte;
        ushort bodyLength = checked((ushort)(CommandHeaderLength + byteCount));
        byte[] body = new byte[BodyPrefixLength + bodyLength];
        FillBodyHeader(body, station, bodyLength, FunctionWriteBits, address, (ushort)values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                body[WritePayloadOffset + (i / BitsPerByte)] |= (byte)(1 << (i % BitsPerByte));
            }
        }

        return WrapFrame(body);
    }

    private static void FillBodyHeader(
        byte[] body,
        byte station,
        ushort bodyLength,
        byte functionCode,
        VigorAddress address,
        ushort count)
    {
        body[StationOffset] = station;
        BinaryBuffer.WriteUInt16LittleEndian(body, BodyLengthOffset, bodyLength);
        body[FunctionCodeOffset] = functionCode;
        body[DeviceCodeOffset] = address.DeviceCode;
        body[AddressLowOffset] = address.BcdAddress[0];
        body[AddressMiddleOffset] = address.BcdAddress[1];
        body[AddressHighOffset] = address.BcdAddress[2];
        BinaryBuffer.WriteUInt16LittleEndian(body, CountOffset, count);
    }

    private static byte[] WrapFrame(byte[] body)
    {
        byte checksum = 0;
        int dleCount = 0;
        for (int i = 0; i < body.Length; i++)
        {
            byte currentByte = body[i];
            checksum = (byte)(checksum + currentByte);
            if (currentByte == Dle)
            {
                dleCount++;
            }
        }

        byte[] frame = new byte[FrameEnvelopeOverhead + body.Length + dleCount];
        int writeOffset = 0;
        frame[writeOffset++] = Dle;
        frame[writeOffset++] = Stx;

        for (int i = 0; i < body.Length; i++)
        {
            byte currentByte = body[i];
            frame[writeOffset++] = currentByte;
            if (currentByte == Dle)
            {
                frame[writeOffset++] = Dle;
            }
        }

        frame[writeOffset++] = Dle;
        frame[writeOffset++] = Etx;
        BinaryBuffer.WriteAsciiHexByte(frame, writeOffset, checksum);

        return frame;
    }
}
