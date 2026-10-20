using FieldLink.Plc.Protocols.Megmeet;
using FieldLink.Plc.Protocols.Modbus;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Megmeet;

public class MegmeetAddressTests
{
    [Theory]
    [InlineData("X10", ModbusArea.DiscreteInput, 8)]
    [InlineData("Y17", ModbusArea.Coil, 15)]
    [InlineData("M2048", ModbusArea.Coil, 12000)]
    [InlineData("SM256", ModbusArea.Coil, 30000)]
    [InlineData("S1024", ModbusArea.Coil, 31000)]
    public void Parse_BitAddresses_MatchesSpecification(string input, ModbusArea expectedArea, ushort expectedPdu)
    {
        var parsedAddress = MegmeetAddress.Parse(input, isBitAccess: true);
        Assert.Equal(expectedArea, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
        Assert.True(parsedAddress.IsBit);
    }

    [Theory]
    [InlineData("D100", 100)]
    [InlineData("SD256", 12000)]
    [InlineData("Z10", 8510)]
    [InlineData("R10", 13010)]
    [InlineData("C199", 9699)]
    [InlineData("C200", 9700)]
    [InlineData("C201", 9702)]
    [InlineData("C256", 10000)]
    [InlineData("C257", 10002)]
    public void Parse_WordAddresses_MatchesSpecification(string input, ushort expectedPdu)
    {
        var parsedAddress = MegmeetAddress.Parse(input, isBitAccess: false);
        Assert.Equal(ModbusArea.HoldingRegister, parsedAddress.Area);
        Assert.Equal(expectedPdu, parsedAddress.PduAddress);
        Assert.False(parsedAddress.IsBit);
    }
}
