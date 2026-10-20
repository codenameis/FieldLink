using System;
using Xunit;

namespace FieldLink.Plc.Protocols.Modbus.Tests;

public sealed class ModbusAddressTests
{
    // 출처: docs/specs/modbus.md §8-1 "P1" 코일 읽기: 00020 -> PDU 주소 0x0013 (19)
    [Fact]
    public void Parse_VectorP1_ParsesCoilCorrectly()
    {
        var address = ModbusAddress.Parse("00020");

        Assert.Equal(ModbusArea.Coil, address.Area);
        Assert.Equal(20, address.ElementNumber);
        Assert.Equal(19, address.PduAddress);
        Assert.True(address.IsWritable);
        Assert.True(address.IsBit);
    }

    // 출처: docs/specs/modbus.md §8-1 "P2" 입력 접점: 10197 -> PDU 주소 0x00C4 (196)
    [Fact]
    public void Parse_VectorP2_ParsesDiscreteInputCorrectly()
    {
        var address = ModbusAddress.Parse("10197");

        Assert.Equal(ModbusArea.DiscreteInput, address.Area);
        Assert.Equal(197, address.ElementNumber);
        Assert.Equal(196, address.PduAddress);
        Assert.False(address.IsWritable);
        Assert.True(address.IsBit);
    }

    // 출처: docs/specs/modbus.md §8-1 "P3" 보유 레지스터: 40108 -> PDU 주소 0x006B (107)
    [Fact]
    public void Parse_VectorP3_ParsesHoldingRegisterCorrectly()
    {
        var address = ModbusAddress.Parse("40108");

        Assert.Equal(ModbusArea.HoldingRegister, address.Area);
        Assert.Equal(108, address.ElementNumber);
        Assert.Equal(107, address.PduAddress);
        Assert.True(address.IsWritable);
        Assert.False(address.IsBit);
    }

    // 출처: docs/specs/modbus.md §8-1 "P4" 입력 레지스터: 30009 -> PDU 주소 0x0008 (8)
    [Fact]
    public void Parse_VectorP4_ParsesInputRegisterCorrectly()
    {
        var address = ModbusAddress.Parse("30009");

        Assert.Equal(ModbusArea.InputRegister, address.Area);
        Assert.Equal(9, address.ElementNumber);
        Assert.Equal(8, address.PduAddress);
        Assert.False(address.IsWritable);
        Assert.False(address.IsBit);
    }

    [Fact]
    public void Parse_SixDigitAddresses_ParsesCorrectly()
    {
        var coil = ModbusAddress.Parse("000001");
        Assert.Equal(ModbusArea.Coil, coil.Area);
        Assert.Equal(1, coil.ElementNumber);
        Assert.Equal(0, coil.PduAddress);

        var holding = ModbusAddress.Parse("465536");
        Assert.Equal(ModbusArea.HoldingRegister, holding.Area);
        Assert.Equal(65536, holding.ElementNumber);
        Assert.Equal(65535, holding.PduAddress);
    }

    // 출처: docs/specs/modbus.md §8-4 "E8" 잘못된 표기 거절 검증: 20001, 40000, 4001, 4000001, D100
    [Theory]
    [InlineData("20001")]   // 지원하지 않는 영역 접두사
    [InlineData("40000")]   // 원소 번호 0
    [InlineData("400000")]  // 6자리 원소 번호 0
    [InlineData("4001")]    // 4자리 (너무 짧음)
    [InlineData("4000001")] // 7자리 (너무 긺)
    [InlineData("D100")]    // 비숫자 문자
    [InlineData("465537")]  // 65536 초과
    public void Parse_InvalidAddresses_ThrowsFormatException(string invalidAddress)
    {
        Assert.Throws<FormatException>(() => ModbusAddress.Parse(invalidAddress));
        Assert.False(ModbusAddress.TryParse(invalidAddress, out _));
    }
}
