using FieldLink.Plc.Protocols.GE;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.GE;

public class GESrtpAddressTests
{
    [Theory]
    [InlineData("M1", true, 0x4C, 0)]
    [InlineData("M1", false, 0x16, 0)]
    [InlineData("M100", false, 0x16, 99)]
    [InlineData("R100", false, 0x08, 99)]
    [InlineData("AI10", false, 0x0A, 9)]
    [InlineData("AQ5", false, 0x0C, 4)]
    [InlineData("I1", true, 0x46, 0)]
    [InlineData("Q8", false, 0x12, 7)]
    [InlineData("SA1", false, 0x18, 0)]
    [InlineData("SB2", false, 0x1A, 1)]
    [InlineData("SC3", false, 0x1C, 2)]
    [InlineData("G10", false, 0x38, 9)]
    [InlineData("%R100", false, 0x08, 99)]
    public void Parse_ValidAddresses_MapsToExpectedTypeCodeAndWireAddress(
        string input,
        bool isBitAccess,
        byte expectedTypeCode,
        ushort expectedWire)
    {
        var parsedAddress = GESrtpAddress.Parse(input, isBitAccess);
        Assert.Equal(expectedTypeCode, parsedAddress.MemoryTypeCode);
        Assert.Equal(expectedWire, parsedAddress.WireAddress);
    }

    [Theory]
    [InlineData("R0")]
    [InlineData("M0")]
    [InlineData("Z10")]
    public void Parse_InvalidAddress_ThrowsFormatException(string invalid)
    {
        Assert.Throws<System.FormatException>(() => GESrtpAddress.Parse(invalid));
    }
}
