using System;
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
    public void Parse_WordRegisters_MapsToExpectedDeviceAndNumber(string input, string expectedWire, int expectedNumber)
    {
        var parsedAddress = MewtocolAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.Equal(expectedNumber, parsedAddress.Number);
        Assert.False(parsedAddress.IsBit);
    }

    [Fact]
    public void Offset_ContactAddress_AdvancesAcrossWordBoundaryInHex16()
    {
        var baseAddress = MewtocolAddress.Parse("R10E");

        MewtocolAddress offsetBy1 = baseAddress.Offset(1);
        MewtocolAddress offsetBy2 = baseAddress.Offset(2);

        Assert.Equal("R010F", offsetBy1.WireString);
        Assert.Equal(10, offsetBy1.Number);
        Assert.Equal(15, offsetBy1.BitOffset);

        Assert.Equal("R0110", offsetBy2.WireString);
        Assert.Equal(11, offsetBy2.Number);
        Assert.Equal(0, offsetBy2.BitOffset);
    }

    [Theory]
    [InlineData("DT99999", 1)]
    [InlineData("R999F", 1)]
    [InlineData("T999", 1)]
    [InlineData("C999", 1)]
    public void Offset_ExceedingWireWidth_ThrowsArgumentOutOfRangeException(string addressText, int delta)
    {
        var address = MewtocolAddress.Parse(addressText);
        Assert.Throws<ArgumentOutOfRangeException>(() => address.Offset(delta));
    }

    [Theory]
    [InlineData("D100000")]
    [InlineData("R10000")]
    [InlineData("T1000")]
    [InlineData("R1.16")]
    [InlineData("D 100")]
    public void Parse_InvalidAddress_ThrowsFormatException(string addressText)
    {
        Assert.Throws<FormatException>(() => MewtocolAddress.Parse(addressText));
    }
}
