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
    private const int ConnectionRequestLength = 22;
    private const byte CotpConnectionHeaderLength = 17;
    private const byte TsapParameterLength = 0x02;
    private const byte TpduSizeParameterLength = 0x01;
    private const int SetupCommunicationLength = 25;
    private const ushort SetupParameterLength = 8;
    private const int ReadRequestLength = 31;
    private const ushort VariableItemParameterLength = 14;
    private const int VariableItemParameterOffset = 17;
    private const ushort SingleBitCount = 1;
    private const int DataItemHeaderLength = 4;
    private const int WriteDataValueOffset = 35;
    private const byte SingleItemCount = 0x01;

    /// <summary>COTP 연결 요청 프레임을 만듭니다. (사양서 §3-2)</summary>
    public static byte[] BuildConnectionRequest(S7CommOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        byte[] frame = new byte[ConnectionRequestLength];
        frame[0] = TpktVersion;
        frame[1] = 0x00;
        BinaryBuffer.WriteUInt16BE(frame, 2, ConnectionRequestLength);

        frame[4] = CotpConnectionHeaderLength; // COTP Length
        frame[5] = CotpConnectionRequest;
        frame[6] = 0x00;
        frame[7] = 0x00;
        frame[8] = 0x00;
        frame[9] = 0x01;
        frame[10] = 0x00;

        frame[11] = CallingTsapCode;
        frame[12] = TsapParameterLength;
        BinaryBuffer.WriteUInt16BE(frame, 13, options.LocalTsap);

        frame[15] = CalledTsapCode;
        frame[16] = TsapParameterLength;
        BinaryBuffer.WriteUInt16BE(frame, 17, options.RemoteTsap);

        frame[19] = TpduSizeCode;
        frame[20] = TpduSizeParameterLength;
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

        byte[] frame = new byte[SetupCommunicationLength];
        WriteTpktAndCotpDataHeader(frame, totalFrameLength: SetupCommunicationLength);
        WriteS7Header(frame, pduReference, paramLength: SetupParameterLength, dataLength: 0);

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

        if (address.IsBit && count != SingleBitCount)
        {
            throw new ArgumentException("비트 주소는 요청 하나에 비트 하나만 읽을 수 있습니다.", nameof(count));
        }

        byte[] frame = new byte[ReadRequestLength];
        WriteTpktAndCotpDataHeader(frame, totalFrameLength: ReadRequestLength);
        WriteS7Header(frame, pduReference, paramLength: VariableItemParameterLength, dataLength: 0);
        WriteItemParameters(frame, offset: VariableItemParameterOffset, FunctionReadVariable, address, count);

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

        if (address.IsBit && data.Length != SingleBitCount)
        {
            throw new ArgumentException("비트 주소는 요청 하나에 비트 하나만 쓸 수 있습니다.", nameof(data));
        }

        if (data.Length > ushort.MaxValue / BitsPerByte)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "한 요청에 쓸 수 있는 바이트 수를 넘었습니다.");
        }

        ushort dataSectionLength = (ushort)(DataItemHeaderLength + data.Length);
        ushort totalLength = (ushort)(ReadRequestLength + dataSectionLength);
        byte[] frame = new byte[totalLength];

        WriteTpktAndCotpDataHeader(frame, totalLength);
        WriteS7Header(frame, pduReference, paramLength: VariableItemParameterLength, dataLength: dataSectionLength);
        WriteItemParameters(frame, offset: VariableItemParameterOffset, FunctionWriteVariable, address, (ushort)data.Length);

        frame[ReadRequestLength] = 0x00;
        frame[ReadRequestLength + 1] = address.IsBit ? DataTransportSizeBit : DataTransportSizeByte;
        BinaryBuffer.WriteUInt16BE(frame, ReadRequestLength + 2, address.IsBit ? SingleBitCount : (ushort)(data.Length * BitsPerByte));

        if (address.IsBit)
        {
            frame[WriteDataValueOffset] = data[0] != 0 ? (byte)0x01 : (byte)0x00;
        }
        else
        {
            Buffer.BlockCopy(data, 0, frame, WriteDataValueOffset, data.Length);
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
        frame[offset + 1] = SingleItemCount;
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
