using System;
using System.Collections.Generic;

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
    private const int ConcurrentRequestCount = 1;

    /// <summary>COTP 연결 요청 프레임을 만듭니다. (사양서 §3-2)</summary>
    public static byte[] BuildConnectionRequest(S7CommOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var cotp = new List<byte> { 0x00, CotpConnectionRequest, 0x00, 0x00, 0x00, 0x01, 0x00 };
        AddTsapParameter(cotp, CallingTsapCode, options.LocalTsap);
        AddTsapParameter(cotp, CalledTsapCode, options.RemoteTsap);
        cotp.AddRange(new byte[] { TpduSizeCode, 0x01, TpduSizeExponent });
        cotp[0] = (byte)(cotp.Count - 1);

        return WrapWithTpkt(cotp);
    }

    /// <summary>Setup Communication 요청 프레임을 만듭니다. (사양서 §3-6)</summary>
    public static byte[] BuildSetupCommunicationRequest(S7CommOptions options, ushort pduReference)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var parameters = new List<byte> { FunctionSetupCommunication, 0x00 };
        AddUInt16(parameters, ConcurrentRequestCount);
        AddUInt16(parameters, ConcurrentRequestCount);
        AddUInt16(parameters, (ushort)options.RequestedPduLength);

        return WrapDataTransfer(BuildS7Pdu(pduReference, parameters, Array.Empty<byte>()));
    }

    /// <summary>읽기 요청 프레임을 만듭니다. (사양서 §3-7)</summary>
    /// <param name="pduReference">요청마다 증가하는 PDU 참조입니다.</param>
    /// <param name="address">읽기 시작 주소입니다.</param>
    /// <param name="count">읽을 바이트 수입니다. 비트 주소는 1이어야 합니다.</param>
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

        List<byte> parameters = BuildItemParameters(FunctionReadVariable, address, count);
        return WrapDataTransfer(BuildS7Pdu(pduReference, parameters, Array.Empty<byte>()));
    }

    /// <summary>쓰기 요청 프레임을 만듭니다. (사양서 §3-8)</summary>
    /// <param name="pduReference">요청마다 증가하는 PDU 참조입니다.</param>
    /// <param name="address">쓰기 시작 주소입니다.</param>
    /// <param name="data">쓸 바이트입니다. 비트 주소는 1바이트(0 이외는 켜짐)여야 합니다.</param>
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

        List<byte> parameters = BuildItemParameters(FunctionWriteVariable, address, (ushort)data.Length);
        return WrapDataTransfer(BuildS7Pdu(pduReference, parameters, BuildWriteDataSection(address, data)));
    }

    private static List<byte> BuildItemParameters(byte function, S7CommAddress address, ushort count)
    {
        var parameters = new List<byte>
        {
            function,
            0x01,
            VariableSpecification,
            AddressLength,
            SyntaxIdS7Any,
            address.IsBit ? RequestTransportSizeBit : RequestTransportSizeByte,
        };

        AddUInt16(parameters, count);
        AddUInt16(parameters, (ushort)address.DataBlockNumber);
        parameters.Add((byte)address.Area);
        parameters.Add((byte)(address.StartBitAddress >> 16));
        parameters.Add((byte)(address.StartBitAddress >> 8));
        parameters.Add((byte)address.StartBitAddress);
        return parameters;
    }

    private static byte[] BuildWriteDataSection(S7CommAddress address, byte[] data)
    {
        var section = new List<byte> { 0x00, address.IsBit ? DataTransportSizeBit : DataTransportSizeByte };

        // 사양서 §3-8: 바이트 접근의 길이는 비트 수, 비트 접근의 길이는 개수(1)이다.
        AddUInt16(section, address.IsBit ? (ushort)1 : (ushort)(data.Length * BitsPerByte));

        if (address.IsBit)
        {
            section.Add(data[0] != 0 ? (byte)0x01 : (byte)0x00);
        }
        else
        {
            section.AddRange(data);
        }

        return section.ToArray();
    }

    private static List<byte> BuildS7Pdu(ushort pduReference, List<byte> parameters, byte[] dataSection)
    {
        var pdu = new List<byte> { ProtocolId, MessageTypeRequest, 0x00, 0x00 };
        AddUInt16(pdu, pduReference);
        AddUInt16(pdu, (ushort)parameters.Count);
        AddUInt16(pdu, (ushort)dataSection.Length);
        pdu.AddRange(parameters);
        pdu.AddRange(dataSection);
        return pdu;
    }

    private static byte[] WrapDataTransfer(List<byte> s7Pdu)
    {
        var cotp = new List<byte> { CotpDataHeaderLength, CotpDataTransfer, CotpLastFragment };
        cotp.AddRange(s7Pdu);
        return WrapWithTpkt(cotp);
    }

    private static byte[] WrapWithTpkt(List<byte> payload)
    {
        var frame = new List<byte> { TpktVersion, 0x00 };
        AddUInt16(frame, (ushort)(payload.Count + 4));
        frame.AddRange(payload);
        return frame.ToArray();
    }

    private static void AddTsapParameter(List<byte> cotp, byte parameterCode, ushort tsap)
    {
        cotp.Add(parameterCode);
        cotp.Add(0x02);
        AddUInt16(cotp, tsap);
    }

    private static void AddUInt16(List<byte> buffer, ushort value)
    {
        // S7 계열은 모든 다중 바이트 값이 빅엔디언이다. (사양서 §5)
        buffer.Add((byte)(value >> 8));
        buffer.Add((byte)value);
    }
}
