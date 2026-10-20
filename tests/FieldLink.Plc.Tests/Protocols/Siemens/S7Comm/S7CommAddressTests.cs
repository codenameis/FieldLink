using System;
using FieldLink.Plc.Protocols.Siemens.S7Comm;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Siemens.S7Comm;

public sealed class S7CommAddressTests
{
    [Theory]
    [InlineData("DB1.DBX0.0", S7CommArea.DataBlock, 1, 0, 0, S7CommAccessSize.Bit)]
    [InlineData("DB1.DBB4", S7CommArea.DataBlock, 1, 4, 0, S7CommAccessSize.Byte)]
    [InlineData("DB1.DBW10", S7CommArea.DataBlock, 1, 10, 0, S7CommAccessSize.Word)]
    [InlineData("db200.dbd20", S7CommArea.DataBlock, 200, 20, 0, S7CommAccessSize.DoubleWord)]
    [InlineData("DB65535.DBX2097151.7", S7CommArea.DataBlock, 65535, 2097151, 7, S7CommAccessSize.Bit)]
    [InlineData("M10.3", S7CommArea.Memory, 0, 10, 3, S7CommAccessSize.Bit)]
    [InlineData("MX10.3", S7CommArea.Memory, 0, 10, 3, S7CommAccessSize.Bit)]
    [InlineData("MB10", S7CommArea.Memory, 0, 10, 0, S7CommAccessSize.Byte)]
    [InlineData("MW10", S7CommArea.Memory, 0, 10, 0, S7CommAccessSize.Word)]
    [InlineData("MD10", S7CommArea.Memory, 0, 10, 0, S7CommAccessSize.DoubleWord)]
    [InlineData("I0.0", S7CommArea.ProcessInput, 0, 0, 0, S7CommAccessSize.Bit)]
    [InlineData("IW0", S7CommArea.ProcessInput, 0, 0, 0, S7CommAccessSize.Word)]
    [InlineData("Q1.7", S7CommArea.ProcessOutput, 0, 1, 7, S7CommAccessSize.Bit)]
    [InlineData("QD4", S7CommArea.ProcessOutput, 0, 4, 0, S7CommAccessSize.DoubleWord)]
    [InlineData("  DB1.DBW10  ", S7CommArea.DataBlock, 1, 10, 0, S7CommAccessSize.Word)]
    public void Parse_ValidAddress_ReadsAllFields(
        string input,
        S7CommArea expectedArea,
        int expectedBlock,
        int expectedByteOffset,
        int expectedBitNumber,
        S7CommAccessSize expectedSize)
    {
        var address = S7CommAddress.Parse(input);

        Assert.Equal(expectedArea, address.Area);
        Assert.Equal(expectedBlock, address.DataBlockNumber);
        Assert.Equal(expectedByteOffset, address.ByteOffset);
        Assert.Equal(expectedBitNumber, address.BitNumber);
        Assert.Equal(expectedSize, address.AccessSize);
    }

    [Theory]
    [InlineData("DB1.DBW10", 80)]
    [InlineData("MB10", 80)]
    [InlineData("M10.3", 83)]
    [InlineData("DB1.DBX0.7", 7)]
    public void StartBitAddress_FollowsSpecRule(string input, int expectedBitAddress)
    {
        Assert.Equal(expectedBitAddress, S7CommAddress.Parse(input).StartBitAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DB0.DBW0")]
    [InlineData("DB65536.DBW0")]
    [InlineData("DB1.DBW")]
    [InlineData("DB1.DBX0")]
    [InlineData("DB1.DBX0.8")]
    [InlineData("DB1.DBW10.1")]
    [InlineData("DB1.DBW-1")]
    [InlineData("DB1.DBB2097152")]
    [InlineData("M10")]
    [InlineData("MB10.0")]
    [InlineData("M10.8")]
    [InlineData("T1")]
    [InlineData("C1")]
    [InlineData("X10.0")]
    [InlineData("DB1")]
    [InlineData("MW99999999999")]
    public void Parse_InvalidAddress_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => S7CommAddress.Parse(input));
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => S7CommAddress.Parse(null!));
    }

    [Fact]
    public void WithOffset_ByteAddress_AddsBytes()
    {
        S7CommAddress shifted = S7CommAddress.Parse("DB1.DBW10").WithOffset(240);

        Assert.Equal(250, shifted.ByteOffset);
        Assert.Equal(1, shifted.DataBlockNumber);
    }

    [Fact]
    public void WithOffset_BitAddress_CarriesIntoNextByte()
    {
        S7CommAddress shifted = S7CommAddress.Parse("M10.6").WithOffset(3);

        Assert.Equal(11, shifted.ByteOffset);
        Assert.Equal(1, shifted.BitNumber);
    }

    [Fact]
    public void WithOffset_BeyondRangeOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7CommAddress.Parse("MB2097151").WithOffset(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7CommAddress.Parse("MB0").WithOffset(-1));
    }

    [Fact]
    public void ReturnCode_EnsureSuccess_ThrowsDeviceExceptionWithCode()
    {
        S7CommReturnCodeExtensions.EnsureSuccess(0xFF);

        var exception = Assert.Throws<PlcDeviceException>(() => S7CommReturnCodeExtensions.EnsureSuccess(0x05));

        Assert.Equal(0x05, exception.DeviceErrorCode);
    }

    [Fact]
    public void ReturnCode_UnknownValue_DescribesCode()
    {
        Assert.Contains("0x77", ((S7CommReturnCode)0x77).GetDescription());
        Assert.Contains("PUT/GET", S7CommReturnCode.AccessNotAllowed.GetDescription());
    }
}
