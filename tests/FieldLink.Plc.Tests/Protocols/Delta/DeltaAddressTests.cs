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
}
