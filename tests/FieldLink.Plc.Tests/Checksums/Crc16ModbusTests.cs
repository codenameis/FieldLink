using System;
using Xunit;

namespace FieldLink.Plc.Checksums.Tests;

public sealed class Crc16ModbusTests
{
    // 출처: docs/specs/modbus.md §8-3 "C1" (매뉴얼: MODBUS over Serial Line Specification V1.02 부록 CRC 예제)
    [Fact]
    public void Calculate_VectorC1_ReturnsExpectedCrc()
    {
        byte[] input = new byte[] { 0x02, 0x07 };

        ushort crc = Crc16Modbus.Calculate(input);
        byte[] wireBytes = Crc16Modbus.CalculateBytes(input, 0, input.Length);

        Assert.Equal(0x1241, crc);
        Assert.Equal(new byte[] { 0x41, 0x12 }, wireBytes);
    }

    // 출처: docs/specs/modbus.md §8-3 "C2" (계산: MODBUS over Serial Line Specification V1.02 규칙)
    [Fact]
    public void Calculate_VectorC2_ReturnsExpectedCrc()
    {
        byte[] input = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };

        ushort crc = Crc16Modbus.Calculate(input);
        byte[] wireBytes = Crc16Modbus.CalculateBytes(input, 0, input.Length);

        Assert.Equal(0xCDC5, crc);
        Assert.Equal(new byte[] { 0xC5, 0xCD }, wireBytes);
    }

    // 출처: docs/specs/modbus.md §8-2 "F2" (요청 ADU 체크섬 검증: 01 03 006B 0003 -> 7417)
    [Fact]
    public void Calculate_VectorF2_ReturnsExpectedCrc()
    {
        byte[] input = new byte[] { 0x01, 0x03, 0x00, 0x6B, 0x00, 0x03 };

        byte[] wireBytes = Crc16Modbus.CalculateBytes(input, 0, input.Length);

        Assert.Equal(new byte[] { 0x74, 0x17 }, wireBytes);
    }

    [Fact]
    public void Calculate_InvalidArguments_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => Crc16Modbus.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Crc16Modbus.Calculate(new byte[5], -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Crc16Modbus.Calculate(new byte[5], 2, 4));
    }
}
