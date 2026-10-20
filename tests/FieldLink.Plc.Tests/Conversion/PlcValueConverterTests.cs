using Xunit;

namespace FieldLink.Plc.Conversion.Tests;

public sealed class PlcValueConverterTests
{
    [Fact]
    public void Int16_ByteOrders_ConvertCorrectly()
    {
        short value = 0x1234;

        byte[] bigEndian = PlcValueConverter.GetBytes(value, ByteOrder.BigEndian);
        byte[] littleEndian = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndian);

        Assert.Equal(new byte[] { 0x12, 0x34 }, bigEndian);
        Assert.Equal(new byte[] { 0x34, 0x12 }, littleEndian);

        Assert.Equal(value, PlcValueConverter.ToInt16(bigEndian, 0, ByteOrder.BigEndian));
        Assert.Equal(value, PlcValueConverter.ToInt16(littleEndian, 0, ByteOrder.LittleEndian));
    }

    [Fact]
    public void UInt32_AllFourByteOrders_ConvertCorrectly()
    {
        uint value = 0x01020304;

        byte[] abcd = PlcValueConverter.GetBytes(value, ByteOrder.BigEndian);
        byte[] cdab = PlcValueConverter.GetBytes(value, ByteOrder.BigEndianWordSwap);
        byte[] dcba = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndian);
        byte[] badc = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndianWordSwap);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, abcd);
        Assert.Equal(new byte[] { 0x03, 0x04, 0x01, 0x02 }, cdab);
        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, dcba);
        Assert.Equal(new byte[] { 0x02, 0x01, 0x04, 0x03 }, badc);

        Assert.Equal(value, PlcValueConverter.ToUInt32(abcd, 0, ByteOrder.BigEndian));
        Assert.Equal(value, PlcValueConverter.ToUInt32(cdab, 0, ByteOrder.BigEndianWordSwap));
        Assert.Equal(value, PlcValueConverter.ToUInt32(dcba, 0, ByteOrder.LittleEndian));
        Assert.Equal(value, PlcValueConverter.ToUInt32(badc, 0, ByteOrder.LittleEndianWordSwap));
    }

    [Fact]
    public void UInt64_AllFourByteOrders_ConvertCorrectly()
    {
        ulong value = 0x0102030405060708;

        byte[] be = PlcValueConverter.GetBytes(value, ByteOrder.BigEndian);
        byte[] beWordSwap = PlcValueConverter.GetBytes(value, ByteOrder.BigEndianWordSwap);
        byte[] le = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndian);
        byte[] leWordSwap = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndianWordSwap);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 }, be);
        Assert.Equal(new byte[] { 0x07, 0x08, 0x05, 0x06, 0x03, 0x04, 0x01, 0x02 }, beWordSwap);
        Assert.Equal(new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 }, le);
        Assert.Equal(new byte[] { 0x02, 0x01, 0x04, 0x03, 0x06, 0x05, 0x08, 0x07 }, leWordSwap);

        Assert.Equal(value, PlcValueConverter.ToUInt64(be, 0, ByteOrder.BigEndian));
        Assert.Equal(value, PlcValueConverter.ToUInt64(beWordSwap, 0, ByteOrder.BigEndianWordSwap));
        Assert.Equal(value, PlcValueConverter.ToUInt64(le, 0, ByteOrder.LittleEndian));
        Assert.Equal(value, PlcValueConverter.ToUInt64(leWordSwap, 0, ByteOrder.LittleEndianWordSwap));
    }

    [Fact]
    public void Single_Roundtrip_PreservesValue()
    {
        float value = 123.456f;

        byte[] bytes = PlcValueConverter.GetBytes(value, ByteOrder.BigEndian);
        float restored = PlcValueConverter.ToSingle(bytes, 0, ByteOrder.BigEndian);

        Assert.Equal(value, restored);
    }

    [Fact]
    public void Double_Roundtrip_PreservesValue()
    {
        double value = 123456.789012;

        byte[] bytes = PlcValueConverter.GetBytes(value, ByteOrder.LittleEndian);
        double restored = PlcValueConverter.ToDouble(bytes, 0, ByteOrder.LittleEndian);

        Assert.Equal(value, restored);
    }

    [Fact]
    public void Arrays_Int32Roundtrip_PreservesAllElements()
    {
        int[] original = new[] { 10, 20, -30, 400000 };

        byte[] bytes = PlcValueConverter.GetBytes(original, ByteOrder.BigEndianWordSwap);
        int[] restored = PlcValueConverter.ToInt32Array(bytes, 0, original.Length, ByteOrder.BigEndianWordSwap);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Boolean_ReadsCorrectBit()
    {
        byte[] buffer = new byte[] { 0b00000100 }; // bit 2 is true

        Assert.False(PlcValueConverter.ToBoolean(buffer, 0, bitIndex: 0));
        Assert.False(PlcValueConverter.ToBoolean(buffer, 0, bitIndex: 1));
        Assert.True(PlcValueConverter.ToBoolean(buffer, 0, bitIndex: 2));
        Assert.False(PlcValueConverter.ToBoolean(buffer, 0, bitIndex: 3));
    }
}
