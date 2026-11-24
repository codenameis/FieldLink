using System;
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
    [InlineData("EB10000", 0x0000, (byte)0x0B, false)]
    [InlineData("H100", 0x1900, (byte)0x00, false)]
    [InlineData("U10", 0x0010, (byte)0x08, false)]
    [InlineData("ES10", 0x0810, (byte)0x00, false)]
    [InlineData("EN10", 0x1010, (byte)0x00, false)]
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

    [Theory]
    [InlineData("EM0", 0x0600, 0)]
    [InlineData("EM15", 0x0601, 5)]
    [InlineData("EK1F", 0x0101, 15)]
    [InlineData("EM1.A", 0x0601, 10)]
    public void Parse_ExtendedBitAddresses_CalculatesWordWireAddressAndBitOffset(
        string input,
        ushort expectedWordWireAddress,
        byte expectedBitOffset)
    {
        var parsedAddress = ToyopucAddress.Parse(input, isBitAccess: true);
        Assert.Equal((byte)0x00, parsedAddress.Prg);
        Assert.True(parsedAddress.IsBit);
        Assert.Equal(expectedWordWireAddress, parsedAddress.WireAddress);
        Assert.Equal(expectedBitOffset, parsedAddress.BitOffset);
    }

    [Theory]
    [InlineData("ES0")]
    [InlineData("EN0")]
    [InlineData("H100")]
    [InlineData("U0")]
    [InlineData("EB0")]
    public void Parse_ExtendedWordOnlyDevicesAsBit_ThrowsFormatException(string address)
    {
        Assert.Throws<FormatException>(() => ToyopucAddress.Parse(address, isBitAccess: true));
    }

    [Theory]
    [InlineData("M10000000.0")]
    [InlineData("M10000001.0")]
    [InlineData("M8000000.0")]
    [InlineData("M 10")]
    [InlineData("M1. 0")]
    [InlineData("M-1")]
    [InlineData("M+1")]
    [InlineData("EB18000")]
    public void Parse_OverflowOrMalformedHexAddresses_ThrowsFormatException(string address)
    {
        Assert.Throws<FormatException>(() => ToyopucAddress.Parse(address, isBitAccess: true));
    }
}
