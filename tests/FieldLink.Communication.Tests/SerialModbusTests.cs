using FieldLink.PlcDrivers.Modbus;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static Task ModbusReferenceCommandSplittingAsync()
    {
        var read = ModbusCommandBuilder.BuildReadModbusCommand("s=2;100", 121, 1, true, 3);
        TestAssert.True(read.IsSuccess); TestAssert.Equal(2, read.Content.Length);
        TestAssert.Bytes(TestBytes.FromHexString("020300640078"), read.Content[0]);
        TestAssert.Bytes(TestBytes.FromHexString("020300DC0001"), read.Content[1]);
        var write = ModbusCommandBuilder.BuildWriteWordModbusCommand("100", (short)17, 1, true, 6, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap));
        TestAssert.Bytes(TestBytes.FromHexString("010600640011"), write.Content);
        TestAssert.Bytes(TestBytes.FromHexString("01030000000AC5CD"), Crc16.Append(TestBytes.FromHexString("01030000000A")));
        var address = ModbusCommandBuilder.AnalysisAddress("s=2;x=4;w=16;100", 1, false, 3);
        TestAssert.Equal(99, address.Content.AddressStart); TestAssert.Equal(16, address.Content.WriteFunction);
        return Task.CompletedTask;
    }
}
