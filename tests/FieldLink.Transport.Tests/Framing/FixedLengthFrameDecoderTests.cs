using System;
using Xunit;

namespace FieldLink.Transport.Framing.Tests;

public sealed class FixedLengthFrameDecoderTests
{
    [Fact]
    public void Constructor_WithZeroOrNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedLengthFrameDecoder(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedLengthFrameDecoder(-1));
    }

    [Fact]
    public void TryGetFrameLength_WhenReceivedFewerBytes_ReturnsFalse()
    {
        var decoder = new FixedLengthFrameDecoder(10);
        var buffer = new byte[8];
        var segment = new ArraySegment<byte>(buffer);

        bool result = decoder.TryGetFrameLength(segment, out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenReceivedExactBytes_ReturnsTrue()
    {
        var decoder = new FixedLengthFrameDecoder(10);
        var buffer = new byte[10];
        var segment = new ArraySegment<byte>(buffer);

        bool result = decoder.TryGetFrameLength(segment, out int frameLength);

        Assert.True(result);
        Assert.Equal(10, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenReceivedMoreBytes_ReturnsTrueWithConfiguredLength()
    {
        var decoder = new FixedLengthFrameDecoder(10);
        var buffer = new byte[15];
        var segment = new ArraySegment<byte>(buffer);

        bool result = decoder.TryGetFrameLength(segment, out int frameLength);

        Assert.True(result);
        Assert.Equal(10, frameLength);
    }
}
