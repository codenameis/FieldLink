using System;
using FieldLink.Plc.Protocols.Keyence;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Keyence;

public class KeyenceHostLinkAddressTests
{
    [Theory]
    [InlineData("DM100", "DM100", false, false)]
    [InlineData("MR100", "MR100", true, false)]
    [InlineData("MR0.5", "MR5", true, false)]
    [InlineData("MR1.5", "MR105", true, false)]
    [InlineData("MR015", "MR15", true, false)]
    [InlineData("W100", "W100", false, true)]
    [InlineData("B20", "B20", true, true)]
    [InlineData("B7FFF", "B7FFF", true, true)]
    [InlineData("VBF9FF", "VBF9FF", true, true)]
    [InlineData("R0", "R0", true, false)]
    [InlineData("CR10", "CR10", true, false)]
    [InlineData("EM50", "EM50", false, false)]
    [InlineData("Z1", "Z1", false, false)]
    [InlineData("Z12", "Z12", false, false)]
    [InlineData("AT7", "AT7", false, false)]
    [InlineData("CTH1", "CTH1", false, false)]
    [InlineData("CTC3", "CTC3", false, false)]
    [InlineData("TM511", "TM511", false, false)]
    [InlineData("DM65534", "DM65534", false, false)]
    public void Parse_ValidAddresses_MatchesExpectedProperties(string input, string expectedWire, bool isBit, bool isHex)
    {
        var parsedAddress = KeyenceHostLinkAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireString);
        Assert.Equal(isBit, parsedAddress.IsBit);
        Assert.Equal(isHex, parsedAddress.IsHex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN100")]
    [InlineData("B1.5")]
    [InlineData("VB10.2")]
    [InlineData("Z0")]
    [InlineData("Z13")]
    [InlineData("AT8")]
    [InlineData("CTH2")]
    [InlineData("CTC4")]
    [InlineData("DM65535")]
    [InlineData("TM512")]
    [InlineData("B8000")]
    [InlineData("W8000")]
    [InlineData("VBFA00")]
    public void Parse_InvalidAddress_ThrowsExpectedException(string invalid)
    {
        Assert.ThrowsAny<Exception>(() => KeyenceHostLinkAddress.Parse(invalid));
    }
}
