using System;
using System.Text;
using FieldLink.Plc.Checksums;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus 요청 PDU 및 ADU 프레임 바이트를 생성하는 정적 순수 함수 모음입니다. (사양서 §3)</summary>
public static class ModbusRequestBuilder
{
    private const ushort CoilOnValue = 0xFF00;
    private const ushort CoilOffValue = 0x0000;
    private const ushort ModbusTcpProtocolId = 0x0000;
    private const byte AsciiColon = 0x3A; // ':'
    private const byte AsciiCarriageReturn = 0x0D; // '\r'
    private const byte AsciiLineFeed = 0x0A; // '\n'

    /// <summary>영역과 시작 주소 및 수량으로 읽기 요청 PDU를 만듭니다. (FC 0x01~0x04)</summary>
    public static byte[] BuildRead(ModbusArea area, ushort pduAddress, ushort count)
    {
        ModbusFunctionCode functionCode = area switch
        {
            ModbusArea.Coil => ModbusFunctionCode.ReadCoils,
            ModbusArea.DiscreteInput => ModbusFunctionCode.ReadDiscreteInputs,
            ModbusArea.InputRegister => ModbusFunctionCode.ReadInputRegisters,
            ModbusArea.HoldingRegister => ModbusFunctionCode.ReadHoldingRegisters,
            _ => throw new ArgumentOutOfRangeException(nameof(area), area, "지원하지 않는 Modbus 영역입니다.")
        };

        ValidateReadCount(functionCode, count);

        return new byte[]
        {
            (byte)functionCode,
            (byte)(pduAddress >> 8),
            (byte)(pduAddress & 0xFF),
            (byte)(count >> 8),
            (byte)(count & 0xFF)
        };
    }

    /// <summary>단일 코일 쓰기 요청 PDU를 만듭니다. (FC 0x05)</summary>
    public static byte[] BuildWriteSingleCoil(ushort pduAddress, bool value)
    {
        ushort rawValue = value ? CoilOnValue : CoilOffValue;
        return new byte[]
        {
            (byte)ModbusFunctionCode.WriteSingleCoil,
            (byte)(pduAddress >> 8),
            (byte)(pduAddress & 0xFF),
            (byte)(rawValue >> 8),
            (byte)(rawValue & 0xFF)
        };
    }

    /// <summary>단일 보유 레지스터 쓰기 요청 PDU를 만듭니다. (FC 0x06)</summary>
    public static byte[] BuildWriteSingleRegister(ushort pduAddress, ushort value)
    {
        return new byte[]
        {
            (byte)ModbusFunctionCode.WriteSingleRegister,
            (byte)(pduAddress >> 8),
            (byte)(pduAddress & 0xFF),
            (byte)(value >> 8),
            (byte)(value & 0xFF)
        };
    }

    /// <summary>다중 코일 쓰기 요청 PDU를 만듭니다. (FC 0x0F)</summary>
    public static byte[] BuildWriteMultipleCoils(ushort pduAddress, ushort count, byte[] packedCoilBytes)
    {
        if (count is < 1 or > 1968)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "다중 코일 쓰기 수량은 1에서 1968 사이여야 합니다.");
        }

        if (packedCoilBytes is null)
        {
            throw new ArgumentNullException(nameof(packedCoilBytes));
        }

        int expectedByteCount = BitPacking.GetPackedByteCount(count);
        if (packedCoilBytes.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"패킹된 코일 데이터 길이({packedCoilBytes.Length})가 기대 길이({expectedByteCount})와 일치하지 않습니다.",
                nameof(packedCoilBytes));
        }

        byte[] pdu = new byte[6 + expectedByteCount];
        pdu[0] = (byte)ModbusFunctionCode.WriteMultipleCoils;
        pdu[1] = (byte)(pduAddress >> 8);
        pdu[2] = (byte)(pduAddress & 0xFF);
        pdu[3] = (byte)(count >> 8);
        pdu[4] = (byte)(count & 0xFF);
        pdu[5] = (byte)expectedByteCount;
        Array.Copy(packedCoilBytes, 0, pdu, 6, expectedByteCount);

        return pdu;
    }

    /// <summary>다중 보유 레지스터 쓰기 요청 PDU를 만듭니다. (FC 0x10)</summary>
    public static byte[] BuildWriteMultipleRegisters(ushort pduAddress, ushort count, byte[] registerBytes)
    {
        if (count is < 1 or > 123)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "다중 레지스터 쓰기 수량은 1에서 123 사이여야 합니다.");
        }

        if (registerBytes is null)
        {
            throw new ArgumentNullException(nameof(registerBytes));
        }

        int expectedByteCount = count * 2;
        if (registerBytes.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"레지스터 바이트 수({registerBytes.Length})가 기대 바이트 수({expectedByteCount})와 일치하지 않습니다.",
                nameof(registerBytes));
        }

        byte[] pdu = new byte[6 + expectedByteCount];
        pdu[0] = (byte)ModbusFunctionCode.WriteMultipleRegisters;
        pdu[1] = (byte)(pduAddress >> 8);
        pdu[2] = (byte)(pduAddress & 0xFF);
        pdu[3] = (byte)(count >> 8);
        pdu[4] = (byte)(count & 0xFF);
        pdu[5] = (byte)expectedByteCount;
        Array.Copy(registerBytes, 0, pdu, 6, expectedByteCount);

        return pdu;
    }

    /// <summary>PDU에 7바이트 MBAP 헤더를 붙여 Modbus TCP ADU 프레임을 만듭니다.</summary>
    public static byte[] BuildTcpAdu(ushort transactionId, byte unitId, byte[] pdu)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        int lengthField = 1 + pdu.Length; // 유닛 ID 1바이트 + PDU 길이
        byte[] adu = new byte[7 + pdu.Length];

        adu[0] = (byte)(transactionId >> 8);
        adu[1] = (byte)(transactionId & 0xFF);
        adu[2] = (byte)(ModbusTcpProtocolId >> 8);
        adu[3] = (byte)(ModbusTcpProtocolId & 0xFF);
        adu[4] = (byte)(lengthField >> 8);
        adu[5] = (byte)(lengthField & 0xFF);
        adu[6] = unitId;

        Array.Copy(pdu, 0, adu, 7, pdu.Length);
        return adu;
    }

    /// <summary>국번과 PDU 뒤에 2바이트 CRC-16을 덧붙여 Modbus RTU ADU 프레임을 만듭니다.</summary>
    public static byte[] BuildRtuAdu(byte unitId, byte[] pdu)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        int totalLength = 1 + pdu.Length + 2;
        byte[] adu = new byte[totalLength];
        adu[0] = unitId;
        Array.Copy(pdu, 0, adu, 1, pdu.Length);

        byte[] crcBytes = Crc16Modbus.CalculateBytes(adu, 0, 1 + pdu.Length);
        adu[adu.Length - 2] = crcBytes[0];
        adu[adu.Length - 1] = crcBytes[1];

        return adu;
    }

    /// <summary>국번과 PDU, 1바이트 LRC를 16진 ASCII 문자로 인코딩하고 콜론과 CRLF를 붙여 Modbus ASCII ADU를 만듭니다.</summary>
    public static byte[] BuildAsciiAdu(byte unitId, byte[] pdu)
    {
        if (pdu is null)
        {
            throw new ArgumentNullException(nameof(pdu));
        }

        byte[] raw = new byte[1 + pdu.Length];
        raw[0] = unitId;
        Array.Copy(pdu, 0, raw, 1, pdu.Length);

        byte lrc = LongitudinalRedundancyCheck.Calculate(raw);
        string hexBody = AsciiHex.GetString(raw) + lrc.ToString("X2");

        byte[] bodyBytes = Encoding.ASCII.GetBytes(hexBody);
        byte[] adu = new byte[1 + bodyBytes.Length + 2];

        adu[0] = AsciiColon;
        Array.Copy(bodyBytes, 0, adu, 1, bodyBytes.Length);
        adu[adu.Length - 2] = AsciiCarriageReturn;
        adu[adu.Length - 1] = AsciiLineFeed;

        return adu;
    }

    private static void ValidateReadCount(ModbusFunctionCode functionCode, ushort count)
    {
        bool isValid = functionCode switch
        {
            ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs => count is >= 1 and <= 2000,
            ModbusFunctionCode.ReadHoldingRegisters or ModbusFunctionCode.ReadInputRegisters => count is >= 1 and <= 125,
            _ => false
        };

        if (!isValid)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"기능 코드 {functionCode}에 대한 읽기 수량이 유효 범위를 벗어났습니다.");
        }
    }
}
