using FieldLink.Plc.Protocols.FujiElectric;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.FujiElectric;

public class FujiElectricSpbAddressTests
{
    [Theory]
    [InlineData("D100", "0C0001", false)]
    [InlineData("M6.0", "020600", true)]
    [InlineData("X0", "010000", true)]
    [InlineData("Y16", "000100", true)]
    [InlineData("TC5", "040000", true)]
    [InlineData("TN10", "0A1000", false)]
    [InlineData("CN1", "0B0100", false)]
    public void Parse_SpbAddresses_MatchesExpectedWireFormat(string input, string expectedWire, bool isBit)
    {
        var parsedAddress = FujiElectricSpbAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireWordAddress);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }

    [Fact]
    public void FormatSwapped4_And_Unswap4_MatchSpecification()
    {
        Assert.Equal("0001", FujiElectricSpbAddress.FormatSwapped4(100));
        Assert.Equal("0100", FujiElectricSpbAddress.FormatSwapped4(1));
        Assert.Equal("2501", FujiElectricSpbAddress.FormatSwapped4(125));

        Assert.Equal(100, FujiElectricSpbAddress.Unswap4("0001"));
        Assert.Equal(1, FujiElectricSpbAddress.Unswap4("0100"));
        Assert.Equal(125, FujiElectricSpbAddress.Unswap4("2501"));
    }

    [Fact]
    public void BuildBitWriteAddress_M1000_MatchesTestVector()
    {
        // M1000: word 62, bit 8 -> byte 125, bit 0 -> 02 + 2501 + 00 = 02250100
        var parsedAddress = FujiElectricSpbAddress.Parse("M1000");
        Assert.Equal("02250100", parsedAddress.BuildBitWriteAddress());
    }

    [Theory]
    [InlineData("M1.100", 0x02, 100u, false)]
    [InlineData("M3.200", 0x04, 200u, false)]
    [InlineData("M10.50", 0x08, 50u, false)]
    [InlineData("I0.5", 0x01, 0u, true)]
    [InlineData("Q10", 0x01, 10u, false)]
    public void Parse_SphAddresses_MapsToExpectedAreaAndOffset(string input, byte expectedArea, uint expectedWord, bool isBit)
    {
        var parsedAddress = FujiElectricSphAddress.Parse(input);
        Assert.Equal(expectedArea, parsedAddress.AreaCode);
        Assert.Equal(expectedWord, parsedAddress.WordAddress);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }
}
