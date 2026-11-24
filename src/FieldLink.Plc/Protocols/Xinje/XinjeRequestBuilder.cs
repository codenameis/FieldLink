using System;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE 확장 및 표준 Modbus 요청 프레임 조립기입니다. (사양서 Part A §A.1)</summary>
public static class XinjeRequestBuilder
{
    internal const byte ExtendedReadBitsFunctionCode = 0x1E;
    internal const byte ExtendedWriteBitsFunctionCode = 0x1F;
    internal const byte ExtendedReadWordsFunctionCode = 0x20;
    internal const byte ExtendedWriteWordsFunctionCode = 0x21;

    private const int BytesPerWord = 2;
    private const int BitsPerByte = 8;
    private const int ByteShift = 8;
    private const int ByteMask = 0xFF;
    private const int ExtendedWriteHeaderLength = 8;
    private const int MinExtendedCount = 1;
    private const int MaxExtendedWriteWordCount = 125;
    private const int MaxExtendedWriteBitCount = 2000;

    private const int FunctionCodeOffset = 0;
    private const int DeviceCodeOffset = 1;
    private const int AddressHighOffset = 2;
    private const int AddressMidOffset = 3;
    private const int AddressLowOffset = 4;
    private const int CountOffset = 5;
    private const int ByteCountOffset = 7;
    private const int DataPayloadOffset = 8;

    /// <summary>확장 Modbus 연속 읽기 PDU를 생성합니다. (0x1E 비트, 0x20 워드)</summary>
    /// <param name="address">읽을 XINJE 시작 주소입니다.</param>
    /// <param name="count">읽을 비트 또는 워드 수량입니다.</param>
    /// <returns>조립된 7바이트 확장 읽기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedReadPdu(XinjeAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "읽기 개수는 1 이상이어야 합니다.");
        }

        byte functionCode = address.IsBit ? ExtendedReadBitsFunctionCode : ExtendedReadWordsFunctionCode;
        byte[] addressBytes = address.Get24BitAddressBytes();

        return
        [
            functionCode,
            address.ExtendedDeviceCode,
            addressBytes[0],
            addressBytes[1],
            addressBytes[2],
            (byte)((count >> ByteShift) & ByteMask),
            (byte)(count & ByteMask),
        ];
    }

    /// <summary>확장 Modbus 연속 워드 쓰기 PDU를 생성합니다. (0x21)</summary>
    /// <param name="address">쓸 XINJE 워드 시작 주소입니다.</param>
    /// <param name="count">쓸 워드 수량입니다. (1~125)</param>
    /// <param name="data">16비트 빅엔디언 워드 데이터 바이트 배열입니다.</param>
    /// <returns>조립된 확장 워드 쓰기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedWriteWordPdu(XinjeAddress address, ushort count, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count < MinExtendedCount || count > MaxExtendedWriteWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"확장 워드 쓰기 수량은 {MinExtendedCount}~{MaxExtendedWriteWordCount} 사이여야 합니다.");
        }

        int byteCount = count * BytesPerWord;
        if (data is null || data.Length < byteCount)
        {
            throw new ArgumentException("워드 쓰기 데이터 길이가 부족합니다.", nameof(data));
        }

        byte[] addressBytes = address.Get24BitAddressBytes();
        byte[] pdu = new byte[ExtendedWriteHeaderLength + byteCount];

        pdu[FunctionCodeOffset] = ExtendedWriteWordsFunctionCode;
        pdu[DeviceCodeOffset] = address.ExtendedDeviceCode;
        pdu[AddressHighOffset] = addressBytes[0];
        pdu[AddressMidOffset] = addressBytes[1];
        pdu[AddressLowOffset] = addressBytes[2];
        BinaryBuffer.WriteUInt16BigEndian(pdu, CountOffset, count);
        pdu[ByteCountOffset] = (byte)byteCount;
        Buffer.BlockCopy(data, 0, pdu, DataPayloadOffset, byteCount);

        return pdu;
    }

    /// <summary>확장 Modbus 연속 비트 쓰기 PDU를 생성합니다. (0x1F)</summary>
    /// <param name="address">쓸 XINJE 비트 시작 주소입니다.</param>
    /// <param name="count">쓸 비트 수량입니다. (1~2000)</param>
    /// <param name="bits">쓸 불리언 비트 배열입니다.</param>
    /// <returns>조립된 확장 비트 쓰기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedWriteBitPdu(XinjeAddress address, ushort count, bool[] bits)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count < MinExtendedCount || count > MaxExtendedWriteBitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"확장 비트 쓰기 수량은 {MinExtendedCount}~{MaxExtendedWriteBitCount} 사이여야 합니다.");
        }

        if (bits is null || bits.Length < count)
        {
            throw new ArgumentException("비트 쓰기 배열 길이가 부족합니다.", nameof(bits));
        }

        byte[] addressBytes = address.Get24BitAddressBytes();
        int byteCount = BitPacking.GetPackedByteCount(count);
        byte[] pdu = new byte[ExtendedWriteHeaderLength + byteCount];

        pdu[FunctionCodeOffset] = ExtendedWriteBitsFunctionCode;
        pdu[DeviceCodeOffset] = address.ExtendedDeviceCode;
        pdu[AddressHighOffset] = addressBytes[0];
        pdu[AddressMidOffset] = addressBytes[1];
        pdu[AddressLowOffset] = addressBytes[2];
        BinaryBuffer.WriteUInt16BigEndian(pdu, CountOffset, count);
        pdu[ByteCountOffset] = (byte)byteCount;

        for (int i = 0; i < count; i++)
        {
            if (bits[i])
            {
                int byteIndex = DataPayloadOffset + (i / BitsPerByte);
                int bitOffset = i % BitsPerByte;
                pdu[byteIndex] |= (byte)(1 << bitOffset);
            }
        }

        return pdu;
    }

    /// <summary>전송 인코딩에 따라 PDU를 ADU 프레임으로 래핑합니다.</summary>
    /// <param name="pdu">래핑할 요청 PDU 바이트 배열입니다.</param>
    /// <param name="station">슬레이브 국번입니다.</param>
    /// <param name="encoding">RTU 또는 TCP 전송 인코딩입니다.</param>
    /// <param name="transactionId">TCP MBAP 트랜잭션 ID입니다.</param>
    /// <returns>조립된 ADU 프레임 바이트 배열입니다.</returns>
    public static byte[] WrapAdu(byte[] pdu, byte station, XinjeTransportEncoding encoding, ushort transactionId)
    {
        return encoding == XinjeTransportEncoding.Tcp
            ? ModbusRequestBuilder.BuildTcpAdu(transactionId, station, pdu)
            : ModbusRequestBuilder.BuildRtuAdu(station, pdu);
    }
}
