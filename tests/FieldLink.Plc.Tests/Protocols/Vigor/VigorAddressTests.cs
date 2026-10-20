using System;
using FieldLink.Plc.Protocols.Vigor;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Vigor;

public class VigorAddressTests
{
    [Theory]
    [InlineData("D100", 0xA0, new byte[] { 0x00, 0x01, 0x00 }, false, false)]
    [InlineData("D9000", 0xA1, new byte[] { 0x00, 0x00, 0x00 }, false, false)]
    [InlineData("D9010", 0xA1, new byte[] { 0x10, 0x00, 0x00 }, false, false)]
    [InlineData("SD10", 0xA1, new byte[] { 0x10, 0x00, 0x00 }, false, false)]
    [InlineData("M10", 0x92, new byte[] { 0x10, 0x00, 0x00 }, true, false)]
    [InlineData("M9000", 0x94, new byte[] { 0x00, 0x00, 0x00 }, true, false)]
    [InlineData("SM10", 0x94, new byte[] { 0x10, 0x00, 0x00 }, true, false)]
    [InlineData("X012", 0x90, new byte[] { 0x12, 0x00, 0x00 }, true, false)]
    [InlineData("Y005", 0x91, new byte[] { 0x05, 0x00, 0x00 }, true, false)]
    [InlineData("C100", 0xAC, new byte[] { 0x00, 0x01, 0x00 }, false, false)]
    [InlineData("C200", 0xAD, new byte[] { 0x00, 0x02, 0x00 }, false, true)]
    [InlineData("T5", 0xA8, new byte[] { 0x05, 0x00, 0x00 }, false, false)]
    [InlineData("TC5", 0x98, new byte[] { 0x05, 0x00, 0x00 }, true, false)]
    public void Parse_ValidAddresses_MatchesExpected(
        string address,
        byte expectedDevCode,
        byte[] expectedBcd,
        bool expectedBit,
        bool expected32Bit)
    {
        var result = VigorAddress.Parse(address, expectedBit);

        Assert.Equal(expectedDevCode, result.DeviceCode);
        Assert.Equal(expectedBcd, result.BcdAddress);
        Assert.Equal(expectedBit, result.IsBit);
        Assert.Equal(expected32Bit, result.Is32BitWord);
    }

    [Theory]
    [InlineData(100, new byte[] { 0x00, 0x01, 0x00 })]
    [InlineData(10, new byte[] { 0x10, 0x00, 0x00 })]
    [InlineData(8999, new byte[] { 0x99, 0x89, 0x00 })]
    [InlineData(123456, new byte[] { 0x56, 0x34, 0x12 })]
    public void EncodeBcdLe_ValidNumbers_MatchesSpecification(int number, byte[] expected)
    {
        byte[] actual = VigorAddress.EncodeBcdLe(number);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ZZ100")]
    [InlineData("D-1")]
    [InlineData("D1000000")]
    public void Parse_InvalidAddress_ThrowsFormatExceptionOrArgument(string invalid)
    {
        if (string.IsNullOrWhiteSpace(invalid))
        {
            Assert.Throws<ArgumentException>(() => VigorAddress.Parse(invalid));
        }
        else
        {
            Assert.Throws<FormatException>(() => VigorAddress.Parse(invalid));
        }
    }
}
