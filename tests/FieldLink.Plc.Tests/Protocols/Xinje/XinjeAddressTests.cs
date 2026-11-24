using System;
using FieldLink.Plc.Protocols.Xinje;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Xinje;

public class XinjeAddressTests
{
    [Theory]
    [InlineData(XinjeSeries.XC, "X10", true, 16392)]
    [InlineData(XinjeSeries.XC, "Y17", true, 18447)]
    [InlineData(XinjeSeries.XC, "D8000", false, 16384)]
    [InlineData(XinjeSeries.XC, "F8000", false, 26624)]
    [InlineData(XinjeSeries.XdXl, "X10000", true, 20736)]
    [InlineData(XinjeSeries.XdXl, "ID0", false, 20480)]
    public void ToModbusPduAddress_MatchesSpecificationVectors(
        XinjeSeries series,
        string address,
        bool isBit,
        ushort expectedPduAddress)
    {
        var parsedAddress = XinjeAddress.Parse(address, series, isBit);
        ushort actualPdu = parsedAddress.ToModbusPduAddress();
        Assert.Equal(expectedPduAddress, actualPdu);
    }

    [Theory]
    [InlineData("D100", 0x80, new byte[] { 0x00, 0x00, 0x64 }, false)]
    [InlineData("M10", 0x03, new byte[] { 0x00, 0x00, 0x0A }, true)]
    [InlineData("X10", 0x01, new byte[] { 0x00, 0x00, 0x08 }, true)] // 10 octal = 8
    [InlineData("Y17", 0x02, new byte[] { 0x00, 0x00, 0x0F }, true)] // 17 octal = 15
    [InlineData("HD50", 0x88, new byte[] { 0x00, 0x00, 0x32 }, false)]
    public void ExtendedProperties_MatchesSpecification(
        string address,
        byte expectedDevCode,
        byte[] expected24Bit,
        bool expectedBit)
    {
        var parsedAddress = XinjeAddress.Parse(address, XinjeSeries.XdXl, expectedBit);
        Assert.Equal(expectedDevCode, parsedAddress.ExtendedDeviceCode);
        Assert.Equal(expected24Bit, parsedAddress.Get24BitAddressBytes());
        Assert.Equal(expectedBit, parsedAddress.IsBit);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ZZ100")]
    public void Parse_InvalidAddress_ThrowsFormatExceptionOrArgument(string invalid)
    {
        if (string.IsNullOrWhiteSpace(invalid))
        {
            Assert.Throws<ArgumentException>(() => XinjeAddress.Parse(invalid));
        }
        else
        {
            Assert.Throws<FormatException>(() => XinjeAddress.Parse(invalid));
        }
    }

    [Theory]
    [InlineData(XinjeSeries.XdXl, "SEM0", true)]
    [InlineData(XinjeSeries.XdXl, "FS0", false)]
    [InlineData(XinjeSeries.XdXl, "FSD0", false)]
    [InlineData(XinjeSeries.XC, "E0", true)]
    [InlineData(XinjeSeries.XC, "ED0", false)]
    public void ExtendedDeviceCode_UnsupportedDevices_AreNullWithoutAliasing(XinjeSeries series, string address, bool isBit)
    {
        XinjeAddress parsed = XinjeAddress.Parse(address, series, isBit);
        Assert.Null(parsed.ExtendedDeviceCode);
        Assert.False(parsed.IsExtended);
    }

    [Fact]
    public void ExtendedDeviceCode_UsesOfficialXinjeDeviceCodeEnum()
    {
        Assert.Equal((byte)XinjeDeviceCode.D, XinjeAddress.Parse("D100", XinjeSeries.XdXl).ExtendedDeviceCode);
        Assert.Equal((byte)XinjeDeviceCode.M, XinjeAddress.Parse("M10", XinjeSeries.XdXl, isBitAccess: true).ExtendedDeviceCode);
        Assert.Equal((byte)XinjeDeviceCode.SFD, XinjeAddress.Parse("SFD10", XinjeSeries.XdXl).ExtendedDeviceCode);
    }

    [Fact]
    public void ToModbusPduAddress_And_Offset_RejectUnsupportedDevicesAnd24BitOverflow()
    {
        XinjeAddress d20480 = XinjeAddress.Parse("D20480", XinjeSeries.XdXl, isBitAccess: false);
        Assert.Throws<InvalidOperationException>(() => d20480.ToModbusPduAddress());

        XinjeAddress dMax = XinjeAddress.Parse("D16777210", XinjeSeries.XdXl, isBitAccess: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => dMax.Offset(10));

        Assert.False(XinjeAddress.TryParse("HD100", XinjeSeries.XC, isBitAccess: false, out _, out _));
        Assert.False(XinjeAddress.TryParse("D8512", XinjeSeries.XC, isBitAccess: false, out _, out _));
        Assert.False(XinjeAddress.TryParse("M12000", XinjeSeries.XC, isBitAccess: true, out _, out _));
        Assert.False(XinjeAddress.TryParse("FS48", XinjeSeries.XdXl, isBitAccess: false, out _, out _));
    }
}
