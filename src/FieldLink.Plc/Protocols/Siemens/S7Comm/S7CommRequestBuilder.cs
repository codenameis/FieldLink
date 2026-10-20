using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 통신 요청 프레임(TPKT + COTP + S7 PDU)을 만드는 순수 함수 모음입니다. (사양서 §3)</summary>
public static class S7CommRequestBuilder
{
    private const byte TpktVersion = 0x03;
    private const byte CotpConnectionRequest = 0xE0;
    private const byte CotpDataTransfer = 0xF0;
    private const byte CotpLastFragment = 0x80;
    private const byte CotpDataHeaderLength = 0x02;
    private const byte TpduSizeExponent = 0x0A;
    private const byte CallingTsapCode = 0xC1;
    private const byte CalledTsapCode = 0xC2;
    private const byte TpduSizeCode = 0xC0;
    private const byte ProtocolId = 0x32;
    private const byte MessageTypeRequest = 0x01;
    private const byte FunctionSetupCommunication = 0xF0;
    private const byte FunctionReadVariable = 0x04;
    private const byte FunctionWriteVariable = 0x05;
    private const byte VariableSpecification = 0x12;
    private const byte AddressLength = 0x0A;
    private const byte SyntaxIdS7Any = 0x10;
    private const byte RequestTransportSizeBit = 0x01;
    private const byte RequestTransportSizeByte = 0x02;
    private const byte DataTransportSizeBit = 0x03;
    private const byte DataTransportSizeByte = 0x04;
    private const int BitsPerByte = 8;
    private const ushort ConcurrentRequestCount = 1;

    /// <summary>COTP 연결 요청 프레임을 만듭니다. (사양서 §3-2)</summary>
    public static byte[] BuildConnectionRequest(S7CommOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        byte[] frame = new byte[22];
        frame[0] = TpktVersion;
        frame[1] = 0x00;
        BinaryBuffer.WriteUInt16BE(frame, 2, 22);

        frame[4] = 17; // COTP Length
        frame[5] = CotpConnectionRequest;
        frame[6] = 0x00;
        frame[7] = 0x00;
        frame[8] = 0x00;
        frame[9] = 0x01;
        frame[10] = 0x00;

        frame[11] = CallingTsapCode;
        frame[12] = 0x02;
        BinaryBuffer.WriteUInt16BE(frame, 13, options.LocalTsap);

        frame[15] = CalledTsapCode;
        frame[16] = 0x02;
        BinaryBuffer.WriteUInt16BE(frame, 17, options.RemoteTsap);

        frame[19] = TpduSizeCode;
        frame[20] = 0x01;
        frame[21] = TpduSizeExponent;

        return frame;
    }

    /// <summary>Setup Communication 요청 프레임을 만듭니다. (사양서 §3-6)</summary>
    public static byte[] BuildSetupCommunicationRequest(S7CommOptions options, ushort pduReference)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        byte[] frame = new byte[25];
        WriteTpktAndCotpDataHeader(frame, totalFrameLength: 25);
        WriteS7Header(frame, pduReference, paramLength: 8, dataLength: 0);

        frame[17] = FunctionSetupCommunication;
        frame[18] = 0x00;
        BinaryBuffer.WriteUInt16BE(frame, 19, ConcurrentRequestCount);
        BinaryBuffer.WriteUInt16BE(frame, 21, ConcurrentRequestCount);
        BinaryBuffer.WriteUInt16BE(frame, 23, (ushort)options.RequestedPduLength);

        return frame;
    }

    /// <summary>읽기 요청 프레임을 만듭니다. (사양서 §3-7)</summary>
    public static byte[] BuildReadRequest(ushort pduReference, S7CommAddress address, ushort count)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (address.IsBit && count != 1)
        {
            throw new ArgumentException("비트 주소는 요청 하나에 비트 하나만 읽을 수 있습니다.", nameof(count));
        }

        byte[] frame = new byte[31];
        WriteTpktAndCotpDataHeader(frame, totalFrameLength: 31);
        WriteS7Header(frame, pduReference, paramLength: 14, dataLength: 0);
        WriteItemParameters(frame, offset: 17, FunctionReadVariable, address, count);

        return frame;
    }

    /// <summary>쓰기 요청 프레임을 만듭니다. (사양서 §3-8)</summary>
    public static byte[] BuildWriteRequest(ushort pduReference, S7CommAddress address, byte[] data)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("쓸 데이터가 비어 있습니다.", nameof(data));
        }

        if (address.IsBit && data.Length != 1)
        {
            throw new ArgumentException("비트 주소는 요청 하나에 비트 하나만 쓸 수 있습니다.", nameof(data));
        }

        if (data.Length * BitsPerByte > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "한 요청에 쓸 수 있는 바이트 수를 넘었습니다.");
        }

        ushort dataSectionLength = (ushort)(4 + data.Length);
        ushort totalLength = (ushort)(31 + dataSectionLength);
        byte[] frame = new byte[totalLength];

        WriteTpktAndCotpDataHeader(frame, totalLength);
        WriteS7Header(frame, pduReference, paramLength: 14, dataLength: dataSectionLength);
        WriteItemParameters(frame, offset: 17, FunctionWriteVariable, address, (ushort)data.Length);

        frame[31] = 0x00;
        frame[32] = address.IsBit ? DataTransportSizeBit : DataTransportSizeByte;
        BinaryBuffer.WriteUInt16BE(frame, 33, address.IsBit ? (ushort)1 : (ushort)(data.Length * BitsPerByte));

        if (address.IsBit)
        {
            frame[35] = data[0] != 0 ? (byte)0x01 : (byte)0x00;
        }
        else
        {
            Buffer.BlockCopy(data, 0, frame, 35, data.Length);
        }

        return frame;
    }

    private static void WriteTpktAndCotpDataHeader(byte[] frame, ushort totalFrameLength)
    {
        frame[0] = TpktVersion;
        frame[1] = 0x00;
        BinaryBuffer.WriteUInt16BE(frame, 2, totalFrameLength);
        frame[4] = CotpDataHeaderLength;
        frame[5] = CotpDataTransfer;
        frame[6] = CotpLastFragment;
    }

    private static void WriteS7Header(byte[] frame, ushort pduReference, ushort paramLength, ushort dataLength)
    {
        frame[7] = ProtocolId;
        frame[8] = MessageTypeRequest;
        frame[9] = 0x00;
        frame[10] = 0x00;
        BinaryBuffer.WriteUInt16BE(frame, 11, pduReference);
        BinaryBuffer.WriteUInt16BE(frame, 13, paramLength);
        BinaryBuffer.WriteUInt16BE(frame, 15, dataLength);
    }

    private static void WriteItemParameters(byte[] frame, int offset, byte function, S7CommAddress address, ushort count)
    {
        frame[offset] = function;
        frame[offset + 1] = 0x01;
        frame[offset + 2] = VariableSpecification;
        frame[offset + 3] = AddressLength;
        frame[offset + 4] = SyntaxIdS7Any;
        frame[offset + 5] = address.IsBit ? RequestTransportSizeBit : RequestTransportSizeByte;
        BinaryBuffer.WriteUInt16BE(frame, offset + 6, count);
        BinaryBuffer.WriteUInt16BE(frame, offset + 8, (ushort)address.DataBlockNumber);
        frame[offset + 10] = (byte)address.Area;
        BinaryBuffer.WriteUInt24BE(frame, offset + 11, address.StartBitAddress);
    }
}
