using System;
using FieldLink.Plc.Protocols.Fatek;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Fatek;

public class FatekAddressTests
{
    [Theory]
    [InlineData("X0", "X0000", true, false)]
    [InlineData("X15", "X0015", true, false)]
    [InlineData("Y0", "Y0000", true, false)]
    [InlineData("M100", "M0100", true, false)]
    [InlineData("S20", "S0020", true, false)]
    [InlineData("T5", "T0005", true, false)]
    [InlineData("C10", "C0010", true, false)]
    public void Parse_DiscreteAddresses_FormatsAs4Digits(
        string input,
        string expectedWire,
        bool expectedIsBit,
        bool expectedIsWordGroup)
    {
        var parsedAddress = FatekAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.Equal(expectedIsBit, parsedAddress.IsBit);
        Assert.Equal(expectedIsWordGroup, parsedAddress.IsWordGroup);
    }

    [Theory]
    [InlineData("R0", "R00000")]
    [InlineData("R100", "R00100")]
    [InlineData("D0", "D00000")]
    [InlineData("D500", "D00500")]
    [InlineData("F10", "F00010")]
    public void Parse_WordRegisters_FormatsAs5Digits(string input, string expectedWire)
    {
        var parsedAddress = FatekAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.False(parsedAddress.IsBit);
        Assert.False(parsedAddress.IsWordGroup);
    }

    [Theory]
    [InlineData("WX0", "WX0000")]
    [InlineData("WY16", "WY0016")]
    [InlineData("WM0", "WM0000")]
    [InlineData("WS0", "WS0000")]
    [InlineData("WT0", "WT0000")]
    [InlineData("WC0", "WC0000")]
    public void Parse_WordGroupAddresses_FormatsAs4Digits(string input, string expectedWire)
    {
        var parsedAddress = FatekAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.False(parsedAddress.IsBit);
        Assert.True(parsedAddress.IsWordGroup);
    }

    [Theory]
    [InlineData("RT0", "RT0000")]
    [InlineData("RC5", "RC0005")]
    [InlineData("DRC0", "DRC0000")]
    [InlineData("DRC10", "DRC0010")]
    public void Parse_TimerCounterPresentValue_FormatsAs4Digits(string input, string expectedWire)
    {
        var parsedAddress = FatekAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.False(parsedAddress.IsBit);
        Assert.False(parsedAddress.IsWordGroup);
    }

    [Fact]
    public void Offset_DiscreteAndWord_ComputesCorrectly()
    {
        var bitAddr = FatekAddress.Parse("X10").Offset(5);
        Assert.Equal("X0015", bitAddr.WireString);

        var wordAddr = FatekAddress.Parse("D100").Offset(20);
        Assert.Equal("D00120", wordAddr.WireString);
    }

    [Theory]
    [InlineData("Z10")]
    [InlineData("X")]
    [InlineData("D999999")]
    [InlineData("X10000")]
    public void Parse_InvalidAddress_ThrowsFormatException(string invalid)
    {
        Assert.Throws<FormatException>(() => FatekAddress.Parse(invalid));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyAddress_ThrowsArgumentException(string empty)
    {
        Assert.Throws<ArgumentException>(() => FatekAddress.Parse(empty));
    }
}
