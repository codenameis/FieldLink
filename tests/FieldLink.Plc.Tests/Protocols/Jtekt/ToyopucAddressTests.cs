using FieldLink.Plc.Protocols.Jtekt;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Jtekt;

public class ToyopucAddressTests
{
    [Theory]
    [InlineData("D100", 0x1100, null, false)]
    [InlineData("M10", 0x1810, null, true)]
    [InlineData("K10", 0x0030, null, false)]
    [InlineData("EK10", 0x0110, (byte)0x00, false)]
    [InlineData("EB9000", 0x1000, (byte)0x0A, false)]
    [InlineData("H100", 0x1900, (byte)0x00, false)]
    public void Parse_ValidAddresses_MatchesSpecification(
        string input,
        ushort expectedWire,
        byte? expectedPrg,
        bool isBit)
    {
        var parsedAddress = ToyopucAddress.Parse(input, isBit);
        Assert.Equal(expectedWire, parsedAddress.WireAddress);
        Assert.Equal(expectedPrg, parsedAddress.Prg);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }
}
