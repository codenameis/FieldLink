using System;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance 표준 및 확장 기능 코드(FC + 0x30) 요청 PDU와 ADU 프레임을 조립합니다. (사양서 §1.2, §1.3)</summary>
public static class InovanceRequestBuilder
{
    internal const byte ExtendedFunctionCodeOffset = 0x30;

    private const int FunctionCodeIndex = 0;

    /// <summary>주소의 확장 기능 코드 여부에 맞춰 Modbus 읽기 요청 PDU를 생성합니다.</summary>
    /// <param name="address">대상 Inovance 주소입니다.</param>
    /// <param name="pduAddress">요청할 16비트 Modbus PDU 주소입니다.</param>
    /// <param name="quantity">읽을 비트 또는 워드 수량입니다.</param>
    /// <returns>조립된 요청 PDU 바이트 배열입니다.</returns>
    public static byte[] BuildReadPdu(InovanceAddress address, ushort pduAddress, ushort quantity)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, pduAddress, quantity);
        return ApplyExtendedFunctionCode(pdu, address.UsesExtendedFunctionCode);
    }

    /// <summary>단일 코일 쓰기 요청 PDU(FC 0x05 또는 확장 0x35)를 생성합니다.</summary>
    /// <param name="address">대상 Inovance 비트 주소입니다.</param>
    /// <param name="pduAddress">요청할 16비트 Modbus PDU 주소입니다.</param>
    /// <param name="value">쓸 비트 값입니다.</param>
    /// <returns>조립된 단일 비트 쓰기 PDU 바이트 배열입니다.</returns>
    public static byte[] BuildWriteSingleCoilPdu(InovanceAddress address, ushort pduAddress, bool value)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] pdu = ModbusRequestBuilder.BuildWriteSingleCoil(pduAddress, value);
        return ApplyExtendedFunctionCode(pdu, address.UsesExtendedFunctionCode);
    }

    /// <summary>단일 레지스터 쓰기 요청 PDU(FC 0x06 또는 확장 0x36)를 생성합니다.</summary>
    /// <param name="address">대상 Inovance 워드 주소입니다.</param>
    /// <param name="pduAddress">요청할 16비트 Modbus PDU 주소입니다.</param>
    /// <param name="value">쓸 16비트 레지스터 값입니다.</param>
    /// <returns>조립된 단일 워드 쓰기 PDU 바이트 배열입니다.</returns>
    public static byte[] BuildWriteSingleRegisterPdu(InovanceAddress address, ushort pduAddress, ushort value)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] pdu = ModbusRequestBuilder.BuildWriteSingleRegister(pduAddress, value);
        return ApplyExtendedFunctionCode(pdu, address.UsesExtendedFunctionCode);
    }

    /// <summary>다중 코일 쓰기 요청 PDU(FC 0x0F 또는 확장 0x3F)를 생성합니다.</summary>
    /// <param name="address">대상 Inovance 비트 주소입니다.</param>
    /// <param name="pduAddress">요청할 16비트 Modbus PDU 주소입니다.</param>
    /// <param name="bitCount">쓸 비트 개수입니다.</param>
    /// <param name="packedCoilBytes">LSB 우선으로 패킹된 비트 데이터 바이트 배열입니다.</param>
    /// <returns>조립된 다중 비트 쓰기 PDU 바이트 배열입니다.</returns>
    public static byte[] BuildWriteMultipleCoilsPdu(
        InovanceAddress address,
        ushort pduAddress,
        ushort bitCount,
        byte[] packedCoilBytes)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] pdu = ModbusRequestBuilder.BuildWriteMultipleCoils(pduAddress, bitCount, packedCoilBytes);
        return ApplyExtendedFunctionCode(pdu, address.UsesExtendedFunctionCode);
    }

    /// <summary>다중 레지스터 쓰기 요청 PDU(FC 0x10 또는 확장 0x40)를 생성합니다.</summary>
    /// <param name="address">대상 Inovance 워드 주소입니다.</param>
    /// <param name="pduAddress">요청할 16비트 Modbus PDU 주소입니다.</param>
    /// <param name="registerCount">쓸 워드(레지스터) 개수입니다.</param>
    /// <param name="registerBytes">16비트 빅엔디언 워드 데이터 바이트 배열입니다.</param>
    /// <returns>조립된 다중 워드 쓰기 PDU 바이트 배열입니다.</returns>
    public static byte[] BuildWriteMultipleRegistersPdu(
        InovanceAddress address,
        ushort pduAddress,
        ushort registerCount,
        byte[] registerBytes)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] pdu = ModbusRequestBuilder.BuildWriteMultipleRegisters(pduAddress, registerCount, registerBytes);
        return ApplyExtendedFunctionCode(pdu, address.UsesExtendedFunctionCode);
    }

    /// <summary>PDU를 인코딩 방식에 맞춰 Modbus RTU 또는 TCP ADU 프레임으로 감쌉니다.</summary>
    /// <param name="pdu">요청 PDU 바이트 배열입니다.</param>
    /// <param name="station">슬레이브 국번입니다.</param>
    /// <param name="encoding">RTU 또는 TCP 전송 인코딩입니다.</param>
    /// <param name="transactionId">TCP MBAP 트랜잭션 ID입니다.</param>
    /// <returns>조립된 ADU 프레임 바이트 배열입니다.</returns>
    public static byte[] WrapAdu(byte[] pdu, byte station, InovanceTransportEncoding encoding, ushort transactionId)
    {
        return encoding switch
        {
            InovanceTransportEncoding.Rtu => ModbusRequestBuilder.BuildRtuAdu(station, pdu),
            InovanceTransportEncoding.Tcp => ModbusRequestBuilder.BuildTcpAdu(transactionId, station, pdu),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "지원하지 않는 Inovance 전송 인코딩입니다."),
        };
    }

    private static byte[] ApplyExtendedFunctionCode(byte[] pdu, bool usesExtendedFunctionCode)
    {
        if (usesExtendedFunctionCode)
        {
            pdu[FunctionCodeIndex] = (byte)(pdu[FunctionCodeIndex] + ExtendedFunctionCodeOffset);
        }

        return pdu;
    }
}
