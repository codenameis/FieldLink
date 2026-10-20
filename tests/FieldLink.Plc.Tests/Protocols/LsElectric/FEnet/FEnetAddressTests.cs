using System;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.LsElectric.FEnet;

public sealed class FEnetAddressTests
{
    [Theory]
    [InlineData("%DW100", 'D', 'W', 100, "%DW100")]
    [InlineData("%MW0", 'M', 'W', 0, "%MW0")]
    [InlineData("%MX15", 'M', 'X', 15, "%MX15")]
    [InlineData("%DB50", 'D', 'B', 50, "%DB50")]
    [InlineData("%PW1", 'P', 'W', 1, "%PW1")]
    [InlineData("D100", 'D', 'W', 100, "%DW100")]
    [InlineData("M50", 'M', 'W', 50, "%MW50")]
    [InlineData("P0", 'P', 'W', 0, "%PW0")]
    [InlineData("K12", 'K', 'W', 12, "%KW12")]
    public void Parse_ValidAddress_ParsesCorrectly(
        string input,
        char expectedArea,
        char expectedSize,
        int expectedOffset,
        string expectedVariableName)
    {
        var address = FEnetAddress.Parse(input);

        Assert.Equal(expectedArea, address.Area);
        Assert.Equal(expectedSize, address.Size);
        Assert.Equal(expectedOffset, address.Offset);
        Assert.Equal(expectedVariableName, address.VariableName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID")]
    [InlineData("%")]
    [InlineData("%D")]
    [InlineData("%DW")]
    [InlineData("%DW-5")]
    [InlineData("D-10")]
    public void Parse_InvalidAddress_ThrowsFormatExceptionOrArgumentNull(string? input)
    {
        if (input is null)
        {
            Assert.Throws<ArgumentNullException>(() => FEnetAddress.Parse(input!));
        }
        else
        {
            Assert.Throws<FormatException>(() => FEnetAddress.Parse(input));
        }
    }

    [Fact]
    public void WithOffset_IncrementsOffsetCorrectly()
    {
        var baseAddress = FEnetAddress.Parse("%DW100");
        var shifted = baseAddress.WithOffset(25);

        Assert.Equal(125, shifted.Offset);
        Assert.Equal("%DW125", shifted.VariableName);
        Assert.Equal('D', shifted.Area);
        Assert.Equal('W', shifted.Size);
    }

    [Fact]
    public void ErrorCode_GetDescription_ReturnsExpectedText()
    {
        Assert.Equal("디바이스(변수) 영역이 잘못되었습니다.", FEnetErrorCode.DeviceAreaError.GetDescription());
        Assert.Equal("헤더 체크섬(BCC) 검증 실패입니다.", FEnetErrorCode.ChecksumError.GetDescription());
        Assert.Equal("정상", FEnetErrorCode.None.GetDescription());
    }
}
