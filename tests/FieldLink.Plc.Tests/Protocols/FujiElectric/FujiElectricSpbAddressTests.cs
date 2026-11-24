using FieldLink.Plc.Protocols.FujiElectric;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.FujiElectric;

public class FujiElectricSpbAddressTests
{
    [Theory]
    [InlineData("D100", "0C0001", false)]
    [InlineData("M6.0", "020600", true)]
    [InlineData("X0", "010000", true)]
    [InlineData("Y16", "000100", true)]
    [InlineData("TC5", "040000", true)]
    [InlineData("TN10", "0A1000", false)]
    [InlineData("CN1", "0B0100", false)]
    public void Parse_SpbAddresses_MatchesExpectedWireFormat(string input, string expectedWire, bool isBit)
    {
        FujiElectricSpbAddress parsedAddress = FujiElectricSpbAddress.Parse(input);
        Assert.Equal(expectedWire, parsedAddress.WireWordAddress);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }

    [Fact]
    public void FormatSwapped4_And_Unswap4_MatchSpecification()
    {
        Assert.Equal("0001", FujiElectricSpbAddress.FormatSwapped4(100));
        Assert.Equal("0100", FujiElectricSpbAddress.FormatSwapped4(1));
        Assert.Equal("2501", FujiElectricSpbAddress.FormatSwapped4(125));

        Assert.Equal(100, FujiElectricSpbAddress.Unswap4("0001"));
        Assert.Equal(1, FujiElectricSpbAddress.Unswap4("0100"));
        Assert.Equal(125, FujiElectricSpbAddress.Unswap4("2501"));
    }

    [Fact]
    public void BuildBitWriteAddress_M1000_MatchesTestVector()
    {
        // 출처: docs/plc-packet-manual/fuji.md §A.3 "M1000 비트 ON" (word 62, bit 8 -> byte 125, bit 0 -> 02250100)
        FujiElectricSpbAddress parsedAddress = FujiElectricSpbAddress.Parse("M1000");
        Assert.Equal("02250100", parsedAddress.BuildBitWriteAddress());
    }

    [Fact]
    public void Offset_SpbBitAddress_AdvancesAcrossWordBoundaryIn16BitUnits()
    {
        // M100: word 6, bit 4 -> +12 bits = M112: word 7, bit 0
        FujiElectricSpbAddress startAddress = FujiElectricSpbAddress.Parse("M100");
        Assert.Equal((ushort)6, startAddress.WordAddress);
        Assert.Equal((byte)4, startAddress.BitOffset);

        FujiElectricSpbAddress nextWordBitAddress = startAddress.Offset(12);
        Assert.Equal((ushort)7, nextWordBitAddress.WordAddress);
        Assert.Equal((byte)0, nextWordBitAddress.BitOffset);
        Assert.Equal("020700", nextWordBitAddress.WireWordAddress);
    }

    [Theory]
    [InlineData("M1.100", 0x02, 100u, false)]
    [InlineData("M3.200", 0x04, 200u, false)]
    [InlineData("M10.50", 0x08, 50u, false)]
    [InlineData("I0.5", 0x01, 0u, true)]
    [InlineData("Q10", 0x01, 10u, false)]
    [InlineData("M1.100.10", 0x02, 100u, true)]
    [InlineData("M1.100.15", 0x02, 100u, true)]
    [InlineData("%MW1.100", 0x02, 100u, false)]
    [InlineData("%MW3.200", 0x04, 200u, false)]
    [InlineData("%MW10.50", 0x08, 50u, false)]
    [InlineData("%IX0.5", 0x01, 0u, true)]
    [InlineData("%QX10", 0x01, 10u, false)]
    public void Parse_SphAddresses_MapsToExpectedAreaAndOffset(string input, byte expectedArea, uint expectedWord, bool isBit)
    {
        FujiElectricSphAddress parsedAddress = FujiElectricSphAddress.Parse(input);
        Assert.Equal(expectedArea, parsedAddress.AreaCode);
        Assert.Equal(expectedWord, parsedAddress.WordAddress);
        Assert.Equal(isBit, parsedAddress.IsBit);
    }

    [Fact]
    public void Parse_Spb_M6_RespectsBitVsWordAccessMode()
    {
        FujiElectricSpbAddress wordAddress = FujiElectricSpbAddress.Parse("M6", isBitAccess: false);
        Assert.False(wordAddress.IsBit);
        Assert.Equal((ushort)6, wordAddress.WordAddress);
        Assert.Equal("020600", wordAddress.WireWordAddress);

        FujiElectricSpbAddress bitAddress = FujiElectricSpbAddress.Parse("M6", isBitAccess: true);
        Assert.True(bitAddress.IsBit);
        Assert.Equal((ushort)0, bitAddress.WordAddress);
        Assert.Equal((byte)6, bitAddress.BitOffset);
        Assert.Equal("020000", bitAddress.WireWordAddress);
    }

    [Fact]
    public void FormatSwapped4_And_BitWriteAddress_Enforce9999LimitAndRejectWhitespace()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => FujiElectricSpbAddress.FormatSwapped4(10000));
        Assert.Throws<System.FormatException>(() => FujiElectricSpbAddress.Unswap4(" 100"));
        Assert.False(FujiElectricSpbAddress.TryParse("D10. 0", out _, out _));
        Assert.False(FujiElectricSpbAddress.TryParse("D10000", out _, out _));

        FujiElectricSpbAddress outOfBitWriteRange = FujiElectricSpbAddress.Parse("D5000.0");
        Assert.Throws<System.ArgumentOutOfRangeException>(() => outOfBitWriteRange.BuildBitWriteAddress());
    }

    [Fact]
    public void Parse_Sph_DecimalBitOffsets10To15_AndRejectsHexBits()
    {
        FujiElectricSphAddress bit10 = FujiElectricSphAddress.Parse("M1.100.10");
        Assert.Equal((byte)10, bit10.BitOffset);

        FujiElectricSphAddress bit15 = FujiElectricSphAddress.Parse("M1.100.15");
        Assert.Equal((byte)15, bit15.BitOffset);

        Assert.Throws<System.FormatException>(() => FujiElectricSphAddress.Parse("M1.100.A"));
        Assert.Throws<System.FormatException>(() => FujiElectricSphAddress.Parse("M1.100.16"));
    }
}
