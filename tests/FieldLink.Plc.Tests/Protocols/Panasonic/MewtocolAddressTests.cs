using FieldLink.Plc.Protocols.Panasonic;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Panasonic;

public class MewtocolAddressTests
{
    [Theory]
    [InlineData("R0", "R0000", true)]
    [InlineData("R100", "R0100", true)]
    [InlineData("R10F", "R010F", true)]
    [InlineData("T5", "T0005", true)]
    [InlineData("C10", "C0010", true)]
    [InlineData("X0", "X0000", true)]
    [InlineData("Y15", "Y0015", true)]
    public void Parse_DiscreteContacts_FormatsAsExpected5Chars(string input, string expectedWire, bool isBit)
    {
        var parsedAddress = MewtocolAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }

    [Theory]
    [InlineData("DT0", "D", 0)]
    [InlineData("DT100", "D", 100)]
    [InlineData("LD5", "L", 5)]
    [InlineData("FL20", "F", 20)]
    [InlineData("D50", "D", 50)]
    public void Parse_WordRegisters_MapsToExpectedDeviceAndNumber(string input, string expectedWire, int expectedNum)
    {
        var parsedAddress = MewtocolAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.Equal(expectedNum, parsedAddress.Number);
        Assert.False(parsedAddress.IsBit);
    }
}
