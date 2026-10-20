using System;
using FieldLink.Plc.Protocols.Yokogawa;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Yokogawa;

public class YokogawaAddressTests
{
    [Theory]
    [InlineData("D100", 0x0004, 100, false)]
    [InlineData("X10", 0x0018, 10, true)]
    [InlineData("Y20", 0x0019, 20, true)]
    [InlineData("I1", 0x0009, 1, true)]
    [InlineData("E5", 0x0005, 5, true)]
    [InlineData("M15", 0x000D, 15, true)]
    [InlineData("T8", 0x0014, 8, true)]
    [InlineData("C9", 0x0003, 9, true)]
    [InlineData("L10", 0x000C, 10, true)]
    [InlineData("B200", 0x0002, 200, false)]
    [InlineData("F50", 0x0006, 50, false)]
    [InlineData("R12", 0x0012, 12, false)]
    [InlineData("V3", 0x0016, 3, false)]
    [InlineData("Z1", 0x001A, 1, false)]
    [InlineData("W99", 0x0017, 99, false)]
    [InlineData("TS5", 0x0020, 5, false)]
    [InlineData("TP6", 0x0021, 6, false)]
    [InlineData("TI7", 0x0025, 7, false)]
    [InlineData("CS8", 0x0030, 8, false)]
    [InlineData("CP12", 0x0031, 12, false)]
    [InlineData("CI13", 0x0035, 13, false)]
    public void Parse_ValidAddresses_MatchesSpecification(
        string address,
        ushort expectedAttr,
        int expectedNumber,
        bool expectedBit)
    {
        var result = YokogawaAddress.Parse(address, expectedBit);

        Assert.Equal(expectedAttr, result.Attribute);
        Assert.Equal(expectedNumber, result.Number);
        Assert.Equal(expectedBit, result.IsBit);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ZZ100")]
    [InlineData("D-5")]
    public void Parse_InvalidAddress_ThrowsFormatExceptionOrArgument(string invalid)
    {
        if (string.IsNullOrWhiteSpace(invalid))
        {
            Assert.Throws<ArgumentException>(() => YokogawaAddress.Parse(invalid));
        }
        else
        {
            Assert.Throws<FormatException>(() => YokogawaAddress.Parse(invalid));
        }
    }
}
