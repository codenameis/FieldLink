using System;
using Xunit;

namespace FieldLink.Plc.Conversion.Tests;

public sealed class BitPackingTests
{
    // 출처: docs/specs/modbus.md §8-1 "P7" 코일 여러 개 쓰기: 10개 코일 = 1,0,1,1,0,0,1,1, 1,0 -> CD 01
    [Fact]
    public void Pack_ModbusVectorP7_ReturnsExpectedBytes()
    {
        bool[] bits = new[]
        {
            true, false, true, true, false, false, true, true, // 0xCD
            true, false                                         // 0x01
        };

        byte[] packed = BitPacking.Pack(bits);

        Assert.Equal(new byte[] { 0xCD, 0x01 }, packed);
    }

    [Fact]
    public void Unpack_ModbusVectorP7_ReturnsOriginalBits()
    {
        byte[] buffer = new byte[] { 0xCD, 0x01 };

        bool[] unpacked = BitPacking.Unpack(buffer, bitCount: 10);

        bool[] expected = new[]
        {
            true, false, true, true, false, false, true, true,
            true, false
        };

        Assert.Equal(expected, unpacked);
    }

    [Fact]
    public void GetPackedByteCount_ReturnsCeilingDiv8()
    {
        Assert.Equal(0, BitPacking.GetPackedByteCount(0));
        Assert.Equal(1, BitPacking.GetPackedByteCount(1));
        Assert.Equal(1, BitPacking.GetPackedByteCount(8));
        Assert.Equal(2, BitPacking.GetPackedByteCount(9));
        Assert.Equal(2, BitPacking.GetPackedByteCount(16));
        Assert.Equal(3, BitPacking.GetPackedByteCount(17));
    }

    [Fact]
    public void Unpack_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitPacking.Unpack(new byte[1], bitCount: 9));
    }
}
