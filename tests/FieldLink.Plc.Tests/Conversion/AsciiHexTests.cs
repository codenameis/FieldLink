using System;
using Xunit;

namespace FieldLink.Plc.Conversion.Tests;

public sealed class AsciiHexTests
{
    [Fact]
    public void GetBytes_ValidHex_ReturnsExpectedBytes()
    {
        byte[] bytes = AsciiHex.GetBytes("0103006B");

        Assert.Equal(new byte[] { 0x01, 0x03, 0x00, 0x6B }, bytes);
    }

    [Fact]
    public void GetBytes_WithSpaces_SanitizesAndParses()
    {
        byte[] bytes = AsciiHex.GetBytes("01 03 00 6B");

        Assert.Equal(new byte[] { 0x01, 0x03, 0x00, 0x6B }, bytes);
    }

    [Fact]
    public void GetBytes_OddLength_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => AsciiHex.GetBytes("123"));
    }

    [Fact]
    public void GetBytes_InvalidHexCharacter_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => AsciiHex.GetBytes("010G"));
    }

    [Fact]
    public void GetString_ValidBytes_ReturnsUppercaseHex()
    {
        byte[] bytes = new byte[] { 0x01, 0x0A, 0xFF, 0x6B };

        string hex = AsciiHex.GetString(bytes);

        Assert.Equal("010AFF6B", hex);
    }
}
