using System;
using Xunit;

namespace FieldLink.Transport.Framing.Tests;

public sealed class AvailableBytesFrameDecoderTests
{
    [Fact]
    public void Constructor_WithNonPositiveMaxLength_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AvailableBytesFrameDecoder(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AvailableBytesFrameDecoder(-1));
    }

    [Fact]
    public void TryGetFrameLength_WhenEmpty_ReturnsFalse()
    {
        var decoder = new AvailableBytesFrameDecoder();
        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(Array.Empty<byte>()), out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenHasBytes_ReturnsFullCount()
    {
        var decoder = new AvailableBytesFrameDecoder();
        byte[] buffer = new byte[] { 0x30, 0x31, 0x41, 0x3D, 0x31 };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer, 1, 3), out int frameLength);

        Assert.True(result);
        Assert.Equal(3, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenExceedsMaximumLength_ThrowsInvalidFrame()
    {
        var decoder = new AvailableBytesFrameDecoder(maximumFrameLength: 4);
        byte[] buffer = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        TransportException exception = Assert.Throws<TransportException>(
            () => decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out _));

        Assert.Equal(TransportFailure.InvalidFrame, exception.Failure);
    }
}
