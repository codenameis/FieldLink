using FieldLink.Plc.Protocols.Keyence;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Keyence;

public class KeyenceHostLinkAddressTests
{
    [Theory]
    [InlineData("DM100", "DM100", false, false)]
    [InlineData("MR100", "MR100", true, false)]
    [InlineData("W100", "W100", false, true)]
    [InlineData("B20", "B20", true, true)]
    [InlineData("R0", "R0", true, false)]
    [InlineData("CR10", "CR10", true, false)]
    [InlineData("EM50", "EM50", false, false)]
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
    public void Parse_InvalidAddress_ThrowsExpectedException(string invalid)
    {
        Assert.ThrowsAny<System.Exception>(() => KeyenceHostLinkAddress.Parse(invalid));
    }
}
