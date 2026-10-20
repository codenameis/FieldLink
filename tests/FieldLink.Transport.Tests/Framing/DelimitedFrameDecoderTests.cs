using System;
using Xunit;

namespace FieldLink.Transport.Framing.Tests;

public sealed class DelimitedFrameDecoderTests
{
    [Fact]
    public void Constructor_WithNullOrEmptyDelimiter_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new DelimitedFrameDecoder(null!));
        Assert.Throws<ArgumentException>(() => new DelimitedFrameDecoder(Array.Empty<byte>()));
    }

    [Fact]
    public void Constructor_WithMaximumLengthSmallerThanDelimiter_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DelimitedFrameDecoder(new byte[] { 0x0D, 0x0A }, maximumFrameLength: 1));
    }

    [Fact]
    public void TryGetFrameLength_WhenDelimiterNotFound_ReturnsFalse()
    {
        var decoder = new DelimitedFrameDecoder(new byte[] { 0x0D, 0x0A });
        var buffer = new byte[] { 0x01, 0x02, 0x03, 0x0D };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenDelimiterFound_ReturnsTrueAndLength()
    {
        var decoder = new DelimitedFrameDecoder(new byte[] { 0x0D, 0x0A });
        var buffer = new byte[] { 0x01, 0x02, 0x03, 0x0D, 0x0A, 0x99 };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.True(result);
        Assert.Equal(5, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenStartDelimiterMatches_DecodesCorrectly()
    {
        var decoder = new DelimitedFrameDecoder(
            endDelimiter: new byte[] { 0x0D, 0x0A },
            maximumFrameLength: 100,
            startDelimiter: new byte[] { 0x3A });

        var buffer = new byte[] { 0x3A, 0x30, 0x31, 0x0D, 0x0A };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.True(result);
        Assert.Equal(5, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenStartDelimiterMismatches_ThrowsInvalidFrame()
    {
        var decoder = new DelimitedFrameDecoder(
            endDelimiter: new byte[] { 0x0D, 0x0A },
            maximumFrameLength: 100,
            startDelimiter: new byte[] { 0x3A });

        var buffer = new byte[] { 0x20, 0x30, 0x31, 0x0D, 0x0A };

        TransportException exception = Assert.Throws<TransportException>(
            () => decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out _));

        Assert.Equal(TransportFailure.InvalidFrame, exception.Failure);
    }

    [Fact]
    public void TryGetFrameLength_WhenExceedsMaximumLength_ThrowsInvalidFrame()
    {
        var decoder = new DelimitedFrameDecoder(new byte[] { 0x0D, 0x0A }, maximumFrameLength: 4);
        var buffer = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        TransportException exception = Assert.Throws<TransportException>(
            () => decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out _));

        Assert.Equal(TransportFailure.InvalidFrame, exception.Failure);
    }
}
