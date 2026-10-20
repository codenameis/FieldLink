using System.Text;
using FieldLink.Plc.Conversion;
using Xunit;

namespace FieldLink.Plc.Protocols.Modbus.Tests;

public sealed class ModbusRequestBuilderTests
{
    // 출처: docs/specs/modbus.md §8-1 "P1" 코일 읽기: 00020, 19개 -> 01 0013 0013
    [Fact]
    public void BuildRead_VectorP1_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("00020");
        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, address.PduAddress, count: 19);

        Assert.Equal("0100130013", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P2" 입력 접점 읽기: 10197, 22개 -> 02 00C4 0016
    [Fact]
    public void BuildRead_VectorP2_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("10197");
        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, address.PduAddress, count: 22);

        Assert.Equal("0200C40016", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P3" 보유 레지스터 읽기: 40108, 3개 -> 03 006B 0003
    [Fact]
    public void BuildRead_VectorP3_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("40108");
        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, address.PduAddress, count: 3);

        Assert.Equal("03006B0003", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P4" 입력 레지스터 읽기: 30009, 1개 -> 04 0008 0001
    [Fact]
    public void BuildRead_VectorP4_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("30009");
        byte[] pdu = ModbusRequestBuilder.BuildRead(address.Area, address.PduAddress, count: 1);

        Assert.Equal("0400080001", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P5" 코일 하나 쓰기: 00173 = ON -> 05 00AC FF00
    [Fact]
    public void BuildWriteSingleCoil_VectorP5_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("00173");
        byte[] pdu = ModbusRequestBuilder.BuildWriteSingleCoil(address.PduAddress, value: true);

        Assert.Equal("0500ACFF00", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P6" 레지스터 하나 쓰기: 40002 = 3 -> 06 0001 0003
    [Fact]
    public void BuildWriteSingleRegister_VectorP6_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("40002");
        byte[] pdu = ModbusRequestBuilder.BuildWriteSingleRegister(address.PduAddress, value: 3);

        Assert.Equal("0600010003", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P7" 코일 여러 개 쓰기: 00020, 10개 = CD 01 -> 0F 0013 000A 02 CD 01
    [Fact]
    public void BuildWriteMultipleCoils_VectorP7_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("00020");
        byte[] packedData = new byte[] { 0xCD, 0x01 };
        byte[] pdu = ModbusRequestBuilder.BuildWriteMultipleCoils(address.PduAddress, count: 10, packedData);

        Assert.Equal("0F0013000A02CD01", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-1 "P8" 레지스터 여러 개 쓰기: 40002, 2개 = 0x000A, 0x0102 -> 10 0001 0002 04 000A 0102
    [Fact]
    public void BuildWriteMultipleRegisters_VectorP8_GeneratesExpectedPdu()
    {
        var address = ModbusAddress.Parse("40002");
        byte[] registerBytes = new byte[] { 0x00, 0x0A, 0x01, 0x02 };
        byte[] pdu = ModbusRequestBuilder.BuildWriteMultipleRegisters(address.PduAddress, count: 2, registerBytes);

        Assert.Equal("100001000204000A0102", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-2 "F1" TCP 프레임: transactionId 1, unitId 1, P3 -> 0001 0000 0006 01 03 006B 0003
    [Fact]
    public void BuildTcpAdu_VectorF1_GeneratesExpectedFrame()
    {
        byte[] pdu = AsciiHex.GetBytes("03006B0003");
        byte[] adu = ModbusRequestBuilder.BuildTcpAdu(transactionId: 1, unitId: 1, pdu);

        Assert.Equal("0001000000060103006B0003", AsciiHex.GetString(adu));
    }

    // 출처: docs/specs/modbus.md §8-2 "F2" RTU 프레임: unitId 1, P3 -> 01 03 006B 0003 7417
    [Fact]
    public void BuildRtuAdu_VectorF2_GeneratesExpectedFrame()
    {
        byte[] pdu = AsciiHex.GetBytes("03006B0003");
        byte[] adu = ModbusRequestBuilder.BuildRtuAdu(unitId: 1, pdu);

        Assert.Equal("0103006B00037417", AsciiHex.GetString(adu));
    }

    // 출처: docs/specs/modbus.md §8-2 "F8" ASCII 프레임: unitId 1, P3 -> :0103006B00038E\r\n
    [Fact]
    public void BuildAsciiAdu_VectorF8_GeneratesExpectedFrame()
    {
        byte[] pdu = AsciiHex.GetBytes("03006B0003");
        byte[] adu = ModbusRequestBuilder.BuildAsciiAdu(unitId: 1, pdu);

        string asciiString = Encoding.ASCII.GetString(adu);
        Assert.Equal(":0103006B00038E\r\n", asciiString);
    }
}
