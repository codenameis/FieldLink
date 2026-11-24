using System;
using FieldLink.Plc.Protocols.Delta;
using FieldLink.Plc.Protocols.Modbus;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Delta;

public class DeltaAddressTests
{
    [Theory]
    [InlineData("M0", 2048, ModbusArea.Coil)]
    [InlineData("M1535", 3583, ModbusArea.Coil)]
    [InlineData("M1536", 45056, ModbusArea.Coil)]
    [InlineData("X10", 1032, ModbusArea.DiscreteInput)]
    [InlineData("Y17", 1295, ModbusArea.Coil)]
    [InlineData("T5", 1541, ModbusArea.HoldingRegister)]
    [InlineData("C3", 3587, ModbusArea.HoldingRegister)]
    [InlineData("D0", 4096, ModbusArea.HoldingRegister)]
    [InlineData("D4095", 8191, ModbusArea.HoldingRegister)]
    [InlineData("D4096", 36864, ModbusArea.HoldingRegister)]
    [InlineData("C199", 3783, ModbusArea.HoldingRegister)]
    [InlineData("C200", 3784, ModbusArea.HoldingRegister)]
    public void Parse_Dvp_ValidAddresses_MapsToExpectedPduAndArea(string input, ushort expectedPdu, ModbusArea expectedArea)
    {
        var parsedAddress = DeltaAddress.Parse(input, DeltaSeries.Dvp);
        Assert.Equal(DeltaSeries.Dvp, parsedAddress.Series);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
        Assert.Equal(expectedArea, parsedAddress.Area);
    }

    [Theory]
    [InlineData("X8")]
    [InlineData("Y19")]
    [InlineData("X18")]
    public void Parse_Dvp_NonOctalInputs_ThrowsFormatException(string invalidOctal)
    {
        Assert.Throws<FormatException>(() => DeltaAddress.Parse(invalidOctal, DeltaSeries.Dvp));
    }

    [Theory]
    [InlineData("M0", 0, ModbusArea.Coil)]
    [InlineData("SM10", 16394, ModbusArea.Coil)]
    [InlineData("S0", 20480, ModbusArea.Coil)]
    [InlineData("X1.3", 24595, ModbusArea.DiscreteInput)]
    [InlineData("X5", 32773, ModbusArea.InputRegister)]
    [InlineData("Y2", 40962, ModbusArea.HoldingRegister)]
    [InlineData("Y2.1", 40993, ModbusArea.Coil)]
    [InlineData("SR10", 49162, ModbusArea.HoldingRegister)]
    [InlineData("E1", 65025, ModbusArea.HoldingRegister)]
    [InlineData("HC0", 64512, ModbusArea.HoldingRegister)]
    public void Parse_As_ValidAddresses_MapsToExpectedPduAndArea(string input, ushort expectedPdu, ModbusArea expectedArea)
    {
        var parsedAddress = DeltaAddress.Parse(input, DeltaSeries.AS);
        Assert.Equal(DeltaSeries.AS, parsedAddress.Series);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
        Assert.Equal(expectedArea, parsedAddress.Area);
    }

    [Theory]
    [InlineData("X1.16")]
    [InlineData("Y1.-1")]
    public void Parse_As_InvalidBitIndices_ThrowsFormatException(string invalidBit)
    {
        Assert.Throws<FormatException>(() => DeltaAddress.Parse(invalidBit, DeltaSeries.AS));
    }

    [Fact]
    public void Parse_As_XAndYWithoutDot_RespectsBitVsWordAccess()
    {
        // 출처: docs/plc-packet-manual/delta.md §4 "Y2 비트 접근 -> 0xA020"
        DeltaAddress y2Bit = DeltaAddress.Parse("Y2", DeltaSeries.AS, isBitAccess: true);
        Assert.Equal(ModbusArea.Coil, y2Bit.Area);
        Assert.Equal((ushort)0xA020, y2Bit.PduAddress);
        Assert.True(y2Bit.IsBit);

        DeltaAddress x2Bit = DeltaAddress.Parse("X2", DeltaSeries.AS, isBitAccess: true);
        Assert.Equal(ModbusArea.DiscreteInput, x2Bit.Area);
        Assert.Equal((ushort)0x6020, x2Bit.PduAddress);
        Assert.True(x2Bit.IsBit);

        DeltaAddress y2Word = DeltaAddress.Parse("Y2", DeltaSeries.AS, isBitAccess: false);
        Assert.Equal(ModbusArea.HoldingRegister, y2Word.Area);
        Assert.Equal((ushort)0xA002, y2Word.PduAddress);
        Assert.False(y2Word.IsBit);
    }

    [Fact]
    public void Parse_As_WordBitAddressing_D100_5_Succeeds()
    {
        DeltaAddress parsed = DeltaAddress.Parse("D100.5", DeltaSeries.AS, isBitAccess: true);
        Assert.Equal(ModbusArea.HoldingRegister, parsed.Area);
        Assert.Equal((ushort)100, parsed.PduAddress);
        Assert.Equal(5, parsed.BitIndex);
        Assert.True(parsed.IsBit);
    }

    [Theory]
    [InlineData("X20000000000")]
    [InlineData("X40000000000")]
    [InlineData("Y77777777777")]
    [InlineData("X400")]
    public void Parse_Dvp_OctalOverflowOrOutOfRange_ThrowsFormatExceptionWithoutWrapping(string input)
    {
        Assert.Throws<FormatException>(() => DeltaAddress.Parse(input, DeltaSeries.Dvp));
    }

    [Theory]
    [InlineData(DeltaSeries.Dvp, "D10000")]
    [InlineData(DeltaSeries.Dvp, "M4096")]
    [InlineData(DeltaSeries.Dvp, "C256")]
    [InlineData(DeltaSeries.AS, "D30000")]
    [InlineData(DeltaSeries.AS, "M8192")]
    public void Parse_OutOfBoundsAddresses_ThrowsFormatException(DeltaSeries series, string input)
    {
        Assert.Throws<FormatException>(() => DeltaAddress.Parse(input, series));
    }
}
