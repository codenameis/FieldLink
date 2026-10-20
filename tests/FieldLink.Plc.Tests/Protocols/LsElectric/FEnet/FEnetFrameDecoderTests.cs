using System;
using FieldLink.Plc.Protocols.LsElectric.FEnet;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.LsElectric.FEnet;

public sealed class FEnetFrameDecoderTests
{
    [Fact]
    public void TryGetFrameLength_WhenBufferTooShort_ReturnsFalse()
    {
        var decoder = new FEnetFrameDecoder();
        byte[] buffer = new byte[19]; // Header needs 20 bytes

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenFullHeaderPresent_ReturnsCalculatedLength()
    {
        var decoder = new FEnetFrameDecoder();
        byte[] buffer = new byte[38];
        // Length field at offset 16~17 in little endian: 18 (0x0012)
        buffer[16] = 0x12;
        buffer[17] = 0x00;

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.True(result);
        Assert.Equal(38, frameLength); // 20 + 18 = 38
    }
}
