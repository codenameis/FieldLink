using System;
using FieldLink.Plc.Conversion;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>Modbus TCP 요청 ADU 를 해석해 응답 ADU 를 만듭니다. (Modbus Application Protocol V1.1b3 §6, §7)</summary>
internal static class ModbusRequestProcessor
{
    private const byte ExceptionFlag = 0x80;
    private const int MbapHeaderLength = 7;
    private const ushort CoilOnValue = 0xFF00;
    private const ushort CoilOffValue = 0x0000;
    private const int MaxReadBitCount = 2000;
    private const int MaxReadRegisterCount = 125;
    private const int MaxWriteBitCount = 1968;
    private const int MaxWriteRegisterCount = 123;

    /// <summary>요청 ADU 를 처리합니다. 응답하지 않아야 하면 null 을 돌려줍니다.</summary>
    public static byte[]? Process(byte[] requestAdu, VirtualPlcMemory memory, VirtualPlcSettings settings)
    {
        if (requestAdu is null)
        {
            throw new ArgumentNullException(nameof(requestAdu));
        }

        if (requestAdu.Length < MbapHeaderLength + 1)
        {
            return null;
        }

        ushort transactionId = (ushort)((requestAdu[0] << 8) | requestAdu[1]);
        byte unitId = requestAdu[6];

        if (settings.UnitId != 0 && unitId != settings.UnitId)
        {
            return null;
        }

        var pdu = new byte[requestAdu.Length - MbapHeaderLength];
        Array.Copy(requestAdu, MbapHeaderLength, pdu, 0, pdu.Length);

        byte[] responsePdu;
        try
        {
            if (settings.ForcedExceptionCode != 0)
            {
                throw new ModbusServerException((ModbusExceptionCode)settings.ForcedExceptionCode);
            }

            responsePdu = ProcessPdu(pdu, memory);
        }
        catch (ModbusServerException exception)
        {
            responsePdu = new[] { (byte)(pdu[0] | ExceptionFlag), (byte)exception.Code };
        }

        return ModbusRequestBuilder.BuildTcpAdu(transactionId, unitId, responsePdu);
    }

    private static byte[] ProcessPdu(byte[] pdu, VirtualPlcMemory memory)
    {
        var functionCode = (ModbusFunctionCode)pdu[0];
        return functionCode switch
        {
            ModbusFunctionCode.ReadCoils => ReadBits(pdu, memory, ModbusArea.Coil),
            ModbusFunctionCode.ReadDiscreteInputs => ReadBits(pdu, memory, ModbusArea.DiscreteInput),
            ModbusFunctionCode.ReadHoldingRegisters => ReadRegisters(pdu, memory, ModbusArea.HoldingRegister),
            ModbusFunctionCode.ReadInputRegisters => ReadRegisters(pdu, memory, ModbusArea.InputRegister),
            ModbusFunctionCode.WriteSingleCoil => WriteSingleCoil(pdu, memory),
            ModbusFunctionCode.WriteSingleRegister => WriteSingleRegister(pdu, memory),
            ModbusFunctionCode.WriteMultipleCoils => WriteMultipleCoils(pdu, memory),
            ModbusFunctionCode.WriteMultipleRegisters => WriteMultipleRegisters(pdu, memory),
            _ => throw new ModbusServerException(ModbusExceptionCode.IllegalFunction),
        };
    }

    private static byte[] ReadBits(byte[] pdu, VirtualPlcMemory memory, ModbusArea area)
    {
        RequirePduLength(pdu, 5);

        int start = ReadUInt16(pdu, 1);
        int count = ReadUInt16(pdu, 3);
        if (count is < 1 or > MaxReadBitCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        var bits = new bool[count];
        if (!memory.TryReadBits(area, start, count, bits))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        byte[] packed = BitPacking.Pack(bits);
        var response = new byte[2 + packed.Length];
        response[0] = pdu[0];
        response[1] = (byte)packed.Length;
        Array.Copy(packed, 0, response, 2, packed.Length);
        return response;
    }

    private static byte[] ReadRegisters(byte[] pdu, VirtualPlcMemory memory, ModbusArea area)
    {
        RequirePduLength(pdu, 5);

        int start = ReadUInt16(pdu, 1);
        int count = ReadUInt16(pdu, 3);
        if (count is < 1 or > MaxReadRegisterCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        var registers = new ushort[count];
        if (!memory.TryReadRegisters(area, start, count, registers))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        var response = new byte[2 + (count * 2)];
        response[0] = pdu[0];
        response[1] = (byte)(count * 2);

        for (int i = 0; i < count; i++)
        {
            response[2 + (i * 2)] = (byte)(registers[i] >> 8);
            response[3 + (i * 2)] = (byte)(registers[i] & 0xFF);
        }

        return response;
    }

    private static byte[] WriteSingleCoil(byte[] pdu, VirtualPlcMemory memory)
    {
        RequirePduLength(pdu, 5);

        int start = ReadUInt16(pdu, 1);
        ushort rawValue = ReadUInt16(pdu, 3);
        if (rawValue is not (CoilOnValue or CoilOffValue))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        if (!memory.TryWriteBits(start, new[] { rawValue == CoilOnValue }))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        return pdu;
    }

    private static byte[] WriteSingleRegister(byte[] pdu, VirtualPlcMemory memory)
    {
        RequirePduLength(pdu, 5);

        int start = ReadUInt16(pdu, 1);
        ushort value = ReadUInt16(pdu, 3);
        if (!memory.TryWriteRegisters(start, new[] { value }))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        return pdu;
    }

    private static byte[] WriteMultipleCoils(byte[] pdu, VirtualPlcMemory memory)
    {
        RequirePduLength(pdu, 6);

        int start = ReadUInt16(pdu, 1);
        int count = ReadUInt16(pdu, 3);
        int byteCount = pdu[5];

        if (count is < 1 or > MaxWriteBitCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        int expectedByteCount = BitPacking.GetPackedByteCount(count);
        if (byteCount != expectedByteCount || pdu.Length < 6 + byteCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        bool[] bits = BitPacking.Unpack(pdu, 6, count);
        if (!memory.TryWriteBits(start, bits))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        return EchoWriteHeader(pdu);
    }

    private static byte[] WriteMultipleRegisters(byte[] pdu, VirtualPlcMemory memory)
    {
        RequirePduLength(pdu, 6);

        int start = ReadUInt16(pdu, 1);
        int count = ReadUInt16(pdu, 3);
        int byteCount = pdu[5];

        if (count is < 1 or > MaxWriteRegisterCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        if (byteCount != count * 2 || pdu.Length < 6 + byteCount)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }

        var values = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = (ushort)((pdu[6 + (i * 2)] << 8) | pdu[7 + (i * 2)]);
        }

        if (!memory.TryWriteRegisters(start, values))
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataAddress);
        }

        return EchoWriteHeader(pdu);
    }

    /// <summary>다중 쓰기 응답은 기능 코드·시작 주소·수량 5바이트를 그대로 되돌려 줍니다.</summary>
    private static byte[] EchoWriteHeader(byte[] pdu) => new[] { pdu[0], pdu[1], pdu[2], pdu[3], pdu[4] };

    private static ushort ReadUInt16(byte[] pdu, int offset) => (ushort)((pdu[offset] << 8) | pdu[offset + 1]);

    private static void RequirePduLength(byte[] pdu, int minimumLength)
    {
        if (pdu.Length < minimumLength)
        {
            throw new ModbusServerException(ModbusExceptionCode.IllegalDataValue);
        }
    }
}
