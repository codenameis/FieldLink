using FieldLink.Plc.Conversion;
using Xunit;

namespace FieldLink.Plc.Protocols.Modbus.Tests;

public sealed class ModbusResponseParserTests
{
    // 출처: docs/specs/modbus.md §8-1 "P1" 코일 읽기 응답: 01 03 CD 6B 05 -> 19개 코일
    [Fact]
    public void ParseReadBits_VectorP1_ExtractsBitsCorrectly()
    {
        byte[] pdu = AsciiHex.GetBytes("0103CD6B05");

        byte[] bitBytes = ModbusResponseParser.ParseReadBits(pdu, ModbusFunctionCode.ReadCoils, expectedBitCount: 19);

        Assert.Equal(19, bitBytes.Length);
        // 기대값: 1,0,1,1,0,0,1,1, 1,1,0,1,0,1,1,0, 1,0,1
        byte[] expected = new byte[]
        {
            1, 0, 1, 1, 0, 0, 1, 1,
            1, 1, 0, 1, 0, 1, 1, 0,
            1, 0, 1
        };
        Assert.Equal(expected, bitBytes);
    }

    // 출처: docs/specs/modbus.md §8-1 "P3" 보유 레지스터 응답: 03 06 022B 0000 0064 -> 3개 레지스터 (555, 0, 100)
    [Fact]
    public void ParseReadRegisters_VectorP3_ExtractsBytesCorrectly()
    {
        byte[] pdu = AsciiHex.GetBytes("0306022B00000064");

        byte[] registers = ModbusResponseParser.ParseReadRegisters(pdu, ModbusFunctionCode.ReadHoldingRegisters, expectedRegisterCount: 3);

        Assert.Equal(new byte[] { 0x02, 0x2B, 0x00, 0x00, 0x00, 0x64 }, registers);
        Assert.Equal(555, PlcValueConverter.ToUInt16(registers, 0, ByteOrder.BigEndian));
        Assert.Equal(0, PlcValueConverter.ToUInt16(registers, 2, ByteOrder.BigEndian));
        Assert.Equal(100, PlcValueConverter.ToUInt16(registers, 4, ByteOrder.BigEndian));
    }

    // 출처: docs/specs/modbus.md §8-1 "P9" 예외 응답: 81 02 -> PlcDeviceException(0x02)
    [Fact]
    public void ValidatePdu_VectorP9_ThrowsPlcDeviceException()
    {
        byte[] pdu = AsciiHex.GetBytes("8102");

        PlcDeviceException exception = Assert.Throws<PlcDeviceException>(
            () => ModbusResponseParser.ParseReadBits(pdu, ModbusFunctionCode.ReadCoils, expectedBitCount: 1));

        Assert.Equal(0x02, exception.DeviceErrorCode);
        Assert.Equal(PlcErrorKind.DeviceError, exception.Kind);
    }

    // 출처: docs/specs/modbus.md §8-2 "F1" TCP 응답 언랩
    [Fact]
    public void UnwrapTcpAdu_VectorF1_ReturnsExpectedPdu()
    {
        byte[] adu = AsciiHex.GetBytes("000100000009010306022B00000064");

        byte[] pdu = ModbusResponseParser.UnwrapTcpAdu(adu, expectedTransactionId: 1, expectedUnitId: 1);

        Assert.Equal("0306022B00000064", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-2 "F2" RTU 응답 언랩
    [Fact]
    public void UnwrapRtuAdu_VectorF2_ReturnsExpectedPdu()
    {
        byte[] adu = AsciiHex.GetBytes("010306022B00000064057A");

        byte[] pdu = ModbusResponseParser.UnwrapRtuAdu(adu, expectedUnitId: 1);

        Assert.Equal("0306022B00000064", AsciiHex.GetString(pdu));
    }

    // 출처: docs/specs/modbus.md §8-4 "E1" RTU CRC 불일치 검증
    [Fact]
    public void UnwrapRtuAdu_VectorE1_ThrowsInvalidResponse()
    {
        // 마지막 바이트가 7A 대신 7B로 변조된 손상 프레임
        byte[] adu = AsciiHex.GetBytes("010306022B00000064057B");

        PlcException exception = Assert.Throws<PlcException>(
            () => ModbusResponseParser.UnwrapRtuAdu(adu, expectedUnitId: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    // 출처: docs/specs/modbus.md §8-4 "E2" 바이트 수 불일치 검증
    [Fact]
    public void ParseReadRegisters_VectorE2_ThrowsInvalidResponse()
    {
        // 3개 레지스터(6바이트)를 기대하는데 응답 바이트 수가 4바이트인 경우
        byte[] pdu = AsciiHex.GetBytes("0304022B0000");

        PlcException exception = Assert.Throws<PlcException>(
            () => ModbusResponseParser.ParseReadRegisters(pdu, ModbusFunctionCode.ReadHoldingRegisters, expectedRegisterCount: 3));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    // 출처: docs/specs/modbus.md §8-4 "E3" TCP 프로토콜 ID != 0 검증
    [Fact]
    public void UnwrapTcpAdu_VectorE3_ThrowsInvalidResponse()
    {
        byte[] adu = AsciiHex.GetBytes("000100010009010306022B00000064");

        PlcException exception = Assert.Throws<PlcException>(
            () => ModbusResponseParser.UnwrapTcpAdu(adu, expectedTransactionId: 1, expectedUnitId: 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }
}
