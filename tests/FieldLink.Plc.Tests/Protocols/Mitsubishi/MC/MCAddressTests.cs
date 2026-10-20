using System;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Mitsubishi.MC;

public sealed class MCAddressTests
{
    [Theory]
    [InlineData("D100", "D", 100u)]
    [InlineData("d100", "D", 100u)]
    [InlineData("M0", "M", 0u)]
    [InlineData("X1F", "X", 0x1Fu)]
    [InlineData("Y20", "Y", 0x20u)]
    [InlineData("W1A", "W", 0x1Au)]
    [InlineData("B0", "B", 0u)]
    [InlineData("R100", "R", 100u)]
    [InlineData("ZR1000", "ZR", 1000u)]
    [InlineData("SD10", "SD", 10u)]
    [InlineData("SM400", "SM", 400u)]
    [InlineData("SW1F", "SW", 0x1Fu)]
    [InlineData("TN0", "TN", 0u)]
    [InlineData("CN5", "CN", 5u)]
    [InlineData("S10", "S", 10u)]
    [InlineData("Z0", "Z", 0u)]
    public void Parse_ValidAddress_FindsDeviceAndNumber(string input, string expectedSymbol, uint expectedNumber)
    {
        var address = MCAddress.Parse(input);

        Assert.Equal(expectedSymbol, address.Device.Symbol);
        Assert.Equal(expectedNumber, address.DeviceNumber);
    }

    [Theory]
    [InlineData("D", false)]
    [InlineData("X", true)]
    public void Device_IsBit_FollowsDeviceKind(string symbol, bool expectedIsBit)
    {
        var address = MCAddress.Parse(symbol + "10");

        Assert.Equal(expectedIsBit, address.IsBit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("D")]
    [InlineData("D-1")]
    [InlineData("D1F")]
    [InlineData("M1A")]
    [InlineData("Q100")]
    [InlineData("100")]
    [InlineData("D99999999999")]
    [InlineData("XG")]
    public void Parse_InvalidAddress_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => MCAddress.Parse(input));
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MCAddress.Parse(null!));
    }

    [Fact]
    public void WithOffset_AddsElementCountToNumber()
    {
        var shifted = MCAddress.Parse("X1F").WithOffset(1);

        Assert.Equal(0x20u, shifted.DeviceNumber);
        Assert.Equal("X", shifted.Device.Symbol);
    }

    [Fact]
    public void WithOffset_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MCAddress.Parse("D0").WithOffset(-1));
    }

    [Fact]
    public void EndCode_GetDescription_KnownAndUnknown()
    {
        Assert.Equal("정상 완료", MCEndCode.Success.GetDescription());
        Assert.Contains("0x1234", ((MCEndCode)0x1234).GetDescription());
    }

    [Fact]
    public void DeviceTable_HasNoDuplicateSymbolsOrCodes()
    {
        var symbols = new System.Collections.Generic.HashSet<string>();
        var codes = new System.Collections.Generic.HashSet<byte>();

        foreach (MCDevice device in MCDevice.GetAll())
        {
            Assert.True(symbols.Add(device.Symbol));
            Assert.True(codes.Add(device.Code));
        }
    }
}
