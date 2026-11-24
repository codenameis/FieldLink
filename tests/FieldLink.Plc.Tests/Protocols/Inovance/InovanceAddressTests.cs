using FieldLink.Plc.Protocols.Inovance;
using FieldLink.Plc.Protocols.Modbus;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Inovance;

public class InovanceAddressTests
{
    [Theory]
    [InlineData("QX0", ModbusArea.Coil, 0)]
    [InlineData("IX10", ModbusArea.DiscreteInput, 10)]
    [InlineData("MD100", ModbusArea.HoldingRegister, 200)]
    [InlineData("MW50", ModbusArea.HoldingRegister, 50)]
    [InlineData("MX10", ModbusArea.HoldingRegister, 5)]
    public void Parse_AmSeries_ValidAddresses_MapsToExpectedPduAndArea(string input, ModbusArea expectedArea, ushort expectedPdu)
    {
        var parsedAddress = InovanceAddress.Parse(input, InovanceSeries.AM);
        Assert.Equal(expectedArea, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
    }

    [Fact]
    public void Parse_AmSeries_MxBitAddressing_CalculatesWordAndBitIndex()
    {
        // MX5.3: byte 5 -> word 2, byte 1 -> bit 11
        var parsedAddress = InovanceAddress.Parse("MX5.3", InovanceSeries.AM, isBitAccess: true);
        Assert.Equal(2, parsedAddress.PduAddress);
        Assert.True(parsedAddress.IsBit);
        Assert.Equal((byte)11, parsedAddress.BitIndexInWord);
    }

    [Theory]
    [InlineData("X10", ModbusArea.Coil, 63496)]
    [InlineData("Y7", ModbusArea.Coil, 64519)]
    [InlineData("M8000", ModbusArea.Coil, 8000)]
    [InlineData("SD10", ModbusArea.HoldingRegister, 9226)]
    [InlineData("R10", ModbusArea.HoldingRegister, 12298)]
    [InlineData("C199", ModbusArea.HoldingRegister, 62663)]
    [InlineData("C200", ModbusArea.HoldingRegister, 63232)]
    public void Parse_H3uSeries_ValidAddresses_MatchesSpecification(string input, ModbusArea expectedArea, ushort expectedPdu)
    {
        var parsedAddress = InovanceAddress.Parse(input, InovanceSeries.H3u);
        Assert.Equal(expectedArea, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
    }

    [Theory]
    [InlineData("B10", ModbusArea.Coil, 12298)]
    [InlineData("R10", ModbusArea.HoldingRegister, 12298)]
    [InlineData("X10", ModbusArea.Coil, 63496)]
    [InlineData("Y20", ModbusArea.Coil, 64528)]
    public void Parse_H5uSeries_ValidAddresses_MatchesSpecification(string input, ModbusArea expectedArea, ushort expectedPdu)
    {
        var parsedAddress = InovanceAddress.Parse(input, InovanceSeries.H5u);
        Assert.Equal(expectedArea, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
    }

    [Theory]
    [InlineData("SMX0", ModbusArea.Coil, 0, true, "SMX", 0)]
    [InlineData("SM8.3", ModbusArea.Coil, 67, true, "SMX", 67)]
    [InlineData("SMX8.3", ModbusArea.Coil, 67, true, "SMX", 67)]
    [InlineData("SDW0", ModbusArea.HoldingRegister, 0, true, "SDW", 0)]
    [InlineData("SD10", ModbusArea.HoldingRegister, 10, true, "SDW", 10)]
    [InlineData("MB0", ModbusArea.HoldingRegister, 0, false, "MB", 0)]
    [InlineData("MB1", ModbusArea.HoldingRegister, 0, false, "MB", 1)]
    [InlineData("MB5", ModbusArea.HoldingRegister, 2, false, "MB", 5)]
    public void Parse_AmSeries_SmxSdwAndMb_MapsAndPopulatesMetadata(
        string input,
        ModbusArea expectedArea,
        ushort expectedPdu,
        bool expectedExtendedFc,
        string expectedDeviceType,
        int expectedNumber)
    {
        var parsedAddress = InovanceAddress.Parse(input, InovanceSeries.AM);
        Assert.Equal(expectedArea, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
        Assert.Equal(expectedExtendedFc, parsedAddress.UsesExtendedFunctionCode);
        Assert.Equal(expectedDeviceType, parsedAddress.DeviceType);
        Assert.Equal(expectedNumber, parsedAddress.Number);
    }

    [Theory]
    [InlineData(InovanceSeries.AM, "QX5.2", "QX", 42)]
    [InlineData(InovanceSeries.AM, "MW10", "MW", 10)]
    [InlineData(InovanceSeries.AM, "MD100", "MD", 100)]
    [InlineData(InovanceSeries.H3u, "C200", "C", 200)]
    [InlineData(InovanceSeries.H5u, "B10", "B", 10)]
    public void Parse_AllSeries_PopulatesDeviceTypeAndNumber(
        InovanceSeries series,
        string input,
        string expectedDeviceType,
        int expectedNumber)
    {
        var parsedAddress = InovanceAddress.Parse(input, series);
        Assert.Equal(expectedDeviceType, parsedAddress.DeviceType);
        Assert.Equal(expectedNumber, parsedAddress.Number);
    }

    [Theory]
    [InlineData(InovanceSeries.AM, "MW65536")]
    [InlineData(InovanceSeries.AM, "MD32768")]
    [InlineData(InovanceSeries.AM, "MB131072")]
    [InlineData(InovanceSeries.H3u, "D8512")]
    [InlineData(InovanceSeries.H3u, "X400")]
    [InlineData(InovanceSeries.H5u, "M8000")]
    [InlineData(InovanceSeries.H5u, "X2000")]
    public void Parse_OutOfBoundsAddresses_ThrowsFormatException(InovanceSeries series, string input)
    {
        Assert.Throws<System.FormatException>(() => InovanceAddress.Parse(input, series));
    }
}
