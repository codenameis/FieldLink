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

    private const byte MinStation = 1;
    private const byte MaxStation = 247;
    private const int BytesPerWord = 2;
    private const int BitsPerByte = 8;
    private const int ByteShift = 8;
    private const int ByteMask = 0xFF;
    private const int ExtendedWriteHeaderLength = 8;
    private const int MinExtendedCount = 1;
    private const int MaxExtendedWordCount = 120; // 사양서 §A.1.5: 0x20/0x21 최대 120워드(240바이트)
    private const int MaxExtendedBitCount = 1920; // 사양서 §A.1.5: 0x1E/0x1F 최대 1920비트(240바이트)

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
    /// <param name="count">읽을 비트(1~1920) 또는 워드(1~120) 수량입니다.</param>
    /// <returns>조립된 7바이트 확장 읽기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedReadPdu(XinjeAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte deviceCode = RequireExtendedDeviceCode(address);
        int maxCount = address.IsBit ? MaxExtendedBitCount : MaxExtendedWordCount;
        if (count < MinExtendedCount || count > maxCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"확장 {(address.IsBit ? "비트" : "워드")} 읽기 개수는 {MinExtendedCount}~{maxCount} 사이여야 합니다.");
        }

        byte functionCode = address.IsBit ? ExtendedReadBitsFunctionCode : ExtendedReadWordsFunctionCode;
        byte[] addressBytes = address.Get24BitAddressBytes();

        return
        [
            functionCode,
            deviceCode,
            addressBytes[0],
            addressBytes[1],
            addressBytes[2],
            (byte)((count >> ByteShift) & ByteMask),
            (byte)(count & ByteMask),
        ];
    }

    /// <summary>확장 Modbus 연속 워드 쓰기 PDU를 생성합니다. (0x21)</summary>
    /// <param name="address">쓸 XINJE 워드 시작 주소입니다.</param>
    /// <param name="count">쓸 워드 수량입니다. (1~120)</param>
    /// <param name="data">16비트 빅엔디언 워드 데이터 바이트 배열입니다.</param>
    /// <returns>조립된 확장 워드 쓰기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedWriteWordPdu(XinjeAddress address, ushort count, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (address.IsBit)
        {
            throw new ArgumentException("확장 워드 쓰기에는 워드 주소만 사용할 수 있습니다.", nameof(address));
        }

        byte deviceCode = RequireExtendedDeviceCode(address);

        if (count < MinExtendedCount || count > MaxExtendedWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"확장 워드 쓰기 수량은 {MinExtendedCount}~{MaxExtendedWordCount} 사이여야 합니다.");
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        int byteCount = count * BytesPerWord;
        if (data.Length != byteCount)
        {
            throw new ArgumentException($"워드 쓰기 데이터 길이({data.Length})가 기대 바이트 수({byteCount})와 일치하지 않습니다.", nameof(data));
        }

        byte[] addressBytes = address.Get24BitAddressBytes();
        byte[] pdu = new byte[ExtendedWriteHeaderLength + byteCount];

        pdu[FunctionCodeOffset] = ExtendedWriteWordsFunctionCode;
        pdu[DeviceCodeOffset] = deviceCode;
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
    /// <param name="count">쓸 비트 수량입니다. (1~1920)</param>
    /// <param name="bits">쓸 불리언 비트 배열입니다.</param>
    /// <returns>조립된 확장 비트 쓰기 요청 PDU입니다.</returns>
    public static byte[] BuildExtendedWriteBitPdu(XinjeAddress address, ushort count, bool[] bits)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!address.IsBit)
        {
            throw new ArgumentException("확장 비트 쓰기에는 비트 주소만 사용할 수 있습니다.", nameof(address));
        }

        byte deviceCode = RequireExtendedDeviceCode(address);

        if (count < MinExtendedCount || count > MaxExtendedBitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"확장 비트 쓰기 수량은 {MinExtendedCount}~{MaxExtendedBitCount} 사이여야 합니다.");
        }

        if (bits is null)
        {
            throw new ArgumentNullException(nameof(bits));
        }

        if (bits.Length != count)
        {
            throw new ArgumentException($"비트 쓰기 배열 길이({bits.Length})가 요청 개수({count})와 일치하지 않습니다.", nameof(bits));
        }

        byte[] addressBytes = address.Get24BitAddressBytes();
        int byteCount = BitPacking.GetPackedByteCount(count);
        byte[] pdu = new byte[ExtendedWriteHeaderLength + byteCount];

        pdu[FunctionCodeOffset] = ExtendedWriteBitsFunctionCode;
        pdu[DeviceCodeOffset] = deviceCode;
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
    /// <param name="station">슬레이브 국번(1~247)입니다.</param>
    /// <param name="encoding">RTU 또는 TCP 전송 인코딩입니다.</param>
    /// <param name="transactionId">TCP MBAP 트랜잭션 ID입니다.</param>
    /// <returns>조립된 ADU 프레임 바이트 배열입니다.</returns>
    public static byte[] WrapAdu(byte[] pdu, byte station, XinjeTransportEncoding encoding, ushort transactionId)
    {
        if (station is < MinStation or > MaxStation)
        {
            throw new ArgumentOutOfRangeException(nameof(station), station, $"국번은 {MinStation}~{MaxStation} 사이여야 합니다.");
        }

        return encoding switch
        {
            XinjeTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, station, pdu),
            XinjeTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(station, pdu),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "지원하지 않는 XINJE 전송 인코딩입니다."),
        };
    }

    private static byte RequireExtendedDeviceCode(XinjeAddress address)
    {
        if (!address.ExtendedDeviceCode.HasValue)
        {
            throw new ArgumentException($"'{address.Device}' 디바이스는 XINJE 확장 Modbus 디바이스 코드를 지원하지 않습니다.", nameof(address));
        }

        return address.ExtendedDeviceCode.Value;
    }
}
