using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 통신 요청 프레임(TPKT + COTP + S7 PDU)을 만드는 순수 함수 모음입니다. (사양서 §3)</summary>
public static class S7CommRequestBuilder
{
    private const byte TpktVersion = 0x03;
    private const byte TpktReserved = 0x00;
    private const int TpktVersionOffset = 0;
    private const int TpktReservedOffset = 1;
    private const int TpktLengthOffset = 2;
    private const byte CotpConnectionRequest = 0xE0;
    private const byte CotpDataTransfer = 0xF0;
    private const byte CotpLastFragment = 0x80;
    private const byte CotpDataHeaderLength = 0x02;
    private const int CotpLengthOffset = 4;
    private const int CotpPduTypeOffset = 5;
    private const int CotpTpduNumberOffset = 6;
    private const int CotpDstRefOffset = 6;
    private const int CotpSrcRefOffset = 8;
    private const int CotpClassOptionOffset = 10;
    private const int CallingTsapParamOffset = 11;
    private const int CalledTsapParamOffset = 15;
    private const int TpduSizeParamOffset = 19;
    private const int ParamLengthRelativeOffset = 1;
    private const int ParamValueRelativeOffset = 2;
    private const ushort CotpDefaultDstRef = 0x0000;
    private const ushort CotpDefaultSrcRef = 0x0001;
    private const byte CotpClass0 = 0x00;
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
    private const int MaxBytesPerRequest = ushort.MaxValue / BitsPerByte;
    private const ushort ConcurrentRequestCount = 1;
    private const int ConnectionRequestLength = 22;
    private const byte CotpConnectionHeaderLength = 17;
    private const byte TsapParameterLength = 0x02;
    private const byte TpduSizeParameterLength = 0x01;
    private const int SetupCommunicationLength = 25;
    private const ushort SetupParameterLength = 8;
    private const int SetupFunctionOffset = 17;
    private const int SetupReservedOffset = 18;
    private const int SetupMaxCallerOffset = 19;
    private const int SetupMaxCalleeOffset = 21;
    private const int SetupPduLengthOffset = 23;
    private const int ReadRequestLength = 31;
    private const ushort VariableItemParameterLength = 14;
    private const int VariableItemParameterOffset = 17;
    private const ushort SingleBitCount = 1;
    private const int DataItemHeaderLength = 4;
    private const int DataItemReservedOffset = 0;
    private const int DataItemTransportSizeOffset = 1;
    private const int DataItemBitLengthOffset = 2;
    private const int WriteDataValueOffset = 35;
    private const byte SingleItemCount = 0x01;
    private const byte ReservedZeroByte = 0x00;
    private const ushort ReservedZeroWord = 0x0000;
    private const byte BitTrueValue = 0x01;
    private const byte BitFalseValue = 0x00;
    private const int S7ProtocolIdOffset = 7;
    private const int S7MessageTypeOffset = 8;
    private const int S7ReservedOffset = 9;
    private const int S7PduReferenceOffset = 11;
    private const int S7ParameterLengthOffset = 13;
    private const int S7DataLengthOffset = 15;
    private const int ItemCountRelativeOffset = 1;
    private const int ItemVariableSpecRelativeOffset = 2;
    private const int ItemAddressLengthRelativeOffset = 3;
    private const int ItemSyntaxIdRelativeOffset = 4;
    private const int ItemTransportSizeRelativeOffset = 5;
    private const int ItemCountValueRelativeOffset = 6;
    private const int ItemDbNumberRelativeOffset = 8;
    private const int ItemAreaRelativeOffset = 10;
    private const int ItemStartBitAddressRelativeOffset = 11;

    /// <summary>COTP 연결 요청 프레임을 만듭니다. (사양서 §3-2)</summary>
    public static byte[] BuildConnectionRequest(S7CommOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        byte[] frame = new byte[ConnectionRequestLength];
        frame[TpktVersionOffset] = TpktVersion;
        frame[TpktReservedOffset] = TpktReserved;
        BinaryBuffer.WriteUInt16BigEndian(frame, TpktLengthOffset, ConnectionRequestLength);

        frame[CotpLengthOffset] = CotpConnectionHeaderLength;
        frame[CotpPduTypeOffset] = CotpConnectionRequest;
        BinaryBuffer.WriteUInt16BigEndian(frame, CotpDstRefOffset, CotpDefaultDstRef);
        BinaryBuffer.WriteUInt16BigEndian(frame, CotpSrcRefOffset, CotpDefaultSrcRef);
        frame[CotpClassOptionOffset] = CotpClass0;

        frame[CallingTsapParamOffset] = CallingTsapCode;
        frame[CallingTsapParamOffset + ParamLengthRelativeOffset] = TsapParameterLength;
        BinaryBuffer.WriteUInt16BigEndian(frame, CallingTsapParamOffset + ParamValueRelativeOffset, options.LocalTsap);

        frame[CalledTsapParamOffset] = CalledTsapCode;
        frame[CalledTsapParamOffset + ParamLengthRelativeOffset] = TsapParameterLength;
        BinaryBuffer.WriteUInt16BigEndian(frame, CalledTsapParamOffset + ParamValueRelativeOffset, options.RemoteTsap);

        frame[TpduSizeParamOffset] = TpduSizeCode;
        frame[TpduSizeParamOffset + ParamLengthRelativeOffset] = TpduSizeParameterLength;
        frame[TpduSizeParamOffset + ParamValueRelativeOffset] = TpduSizeExponent;

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

        frame[SetupFunctionOffset] = FunctionSetupCommunication;
        frame[SetupReservedOffset] = ReservedZeroByte;
        BinaryBuffer.WriteUInt16BigEndian(frame, SetupMaxCallerOffset, ConcurrentRequestCount);
        BinaryBuffer.WriteUInt16BigEndian(frame, SetupMaxCalleeOffset, ConcurrentRequestCount);
        BinaryBuffer.WriteUInt16BigEndian(frame, SetupPduLengthOffset, (ushort)options.RequestedPduLength);

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

        if (count > MaxBytesPerRequest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"한 요청에 읽을 수 있는 최대 바이트 수({MaxBytesPerRequest})를 초과했습니다.");
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

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.Length == 0)
        {
            throw new ArgumentException("쓸 데이터가 비어 있습니다.", nameof(data));
        }

        if (address.IsBit && data.Length != SingleBitCount)
        {
            throw new ArgumentException("비트 주소는 요청 하나에 비트 하나만 쓸 수 있습니다.", nameof(data));
        }

        if (data.Length > MaxBytesPerRequest)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "한 요청에 쓸 수 있는 바이트 수를 넘었습니다.");
        }

        ushort dataSectionLength = (ushort)(DataItemHeaderLength + data.Length);
        ushort totalLength = (ushort)(ReadRequestLength + dataSectionLength);
        byte[] frame = new byte[totalLength];

        WriteTpktAndCotpDataHeader(frame, totalLength);
        WriteS7Header(frame, pduReference, paramLength: VariableItemParameterLength, dataLength: dataSectionLength);
        WriteItemParameters(frame, offset: VariableItemParameterOffset, FunctionWriteVariable, address, (ushort)data.Length);

        frame[ReadRequestLength + DataItemReservedOffset] = ReservedZeroByte;
        frame[ReadRequestLength + DataItemTransportSizeOffset] = address.IsBit ? DataTransportSizeBit : DataTransportSizeByte;
        BinaryBuffer.WriteUInt16BigEndian(
            frame,
            ReadRequestLength + DataItemBitLengthOffset,
            address.IsBit ? SingleBitCount : (ushort)(data.Length * BitsPerByte));

        if (address.IsBit)
        {
            frame[WriteDataValueOffset] = data[0] != 0 ? BitTrueValue : BitFalseValue;
        }
        else
        {
            Buffer.BlockCopy(data, 0, frame, WriteDataValueOffset, data.Length);
        }

        return frame;
    }

    private static void WriteTpktAndCotpDataHeader(byte[] frame, ushort totalFrameLength)
    {
        frame[TpktVersionOffset] = TpktVersion;
        frame[TpktReservedOffset] = TpktReserved;
        BinaryBuffer.WriteUInt16BigEndian(frame, TpktLengthOffset, totalFrameLength);
        frame[CotpLengthOffset] = CotpDataHeaderLength;
        frame[CotpPduTypeOffset] = CotpDataTransfer;
        frame[CotpTpduNumberOffset] = CotpLastFragment;
    }

    private static void WriteS7Header(byte[] frame, ushort pduReference, ushort paramLength, ushort dataLength)
    {
        frame[S7ProtocolIdOffset] = ProtocolId;
        frame[S7MessageTypeOffset] = MessageTypeRequest;
        BinaryBuffer.WriteUInt16BigEndian(frame, S7ReservedOffset, ReservedZeroWord);
        BinaryBuffer.WriteUInt16BigEndian(frame, S7PduReferenceOffset, pduReference);
        BinaryBuffer.WriteUInt16BigEndian(frame, S7ParameterLengthOffset, paramLength);
        BinaryBuffer.WriteUInt16BigEndian(frame, S7DataLengthOffset, dataLength);
    }

    private static void WriteItemParameters(byte[] frame, int offset, byte function, S7CommAddress address, ushort count)
    {
        frame[offset] = function;
        frame[offset + ItemCountRelativeOffset] = SingleItemCount;
        frame[offset + ItemVariableSpecRelativeOffset] = VariableSpecification;
        frame[offset + ItemAddressLengthRelativeOffset] = AddressLength;
        frame[offset + ItemSyntaxIdRelativeOffset] = SyntaxIdS7Any;
        frame[offset + ItemTransportSizeRelativeOffset] = address.IsBit ? RequestTransportSizeBit : RequestTransportSizeByte;
        BinaryBuffer.WriteUInt16BigEndian(frame, offset + ItemCountValueRelativeOffset, count);
        BinaryBuffer.WriteUInt16BigEndian(frame, offset + ItemDbNumberRelativeOffset, (ushort)address.DataBlockNumber);
        frame[offset + ItemAreaRelativeOffset] = (byte)address.Area;
        BinaryBuffer.WriteUInt24BigEndian(frame, offset + ItemStartBitAddressRelativeOffset, address.StartBitAddress);
    }
}
