using System;
using Xunit;

namespace FieldLink.Plc.Checksums.Tests;

public sealed class LongitudinalRedundancyCheckTests
{
    // 출처: docs/specs/modbus.md §8-3 "C3" (계산: MODBUS over Serial Line Specification V1.02 규칙, 합 0x82의 2의 보수)
    [Fact]
    public void Calculate_VectorC3_ReturnsExpectedLrc()
    {
        byte[] input = new byte[] { 0x11, 0x03, 0x00, 0x6B, 0x00, 0x03 };

        byte lrc = LongitudinalRedundancyCheck.Calculate(input);
        string ascii = LongitudinalRedundancyCheck.CalculateAscii(input, 0, input.Length);

        Assert.Equal(0x7E, lrc);
        Assert.Equal("7E", ascii);
    }

    // 출처: docs/specs/modbus.md §8-2 "F8" (요청 ASCII LRC 검증: 01 03 006B 0003 -> 8E)
    [Fact]
    public void Calculate_VectorF8_ReturnsExpectedLrc()
    {
        byte[] input = new byte[] { 0x01, 0x03, 0x00, 0x6B, 0x00, 0x03 };

        byte lrc = LongitudinalRedundancyCheck.Calculate(input);
        string ascii = LongitudinalRedundancyCheck.CalculateAscii(input, 0, input.Length);

        Assert.Equal(0x8E, lrc);
        Assert.Equal("8E", ascii);
    }

    [Fact]
    public void Calculate_InvalidArguments_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => LongitudinalRedundancyCheck.Calculate(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => LongitudinalRedundancyCheck.Calculate(new byte[4], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LongitudinalRedundancyCheck.Calculate(new byte[4], 2, 3));
    }
}
