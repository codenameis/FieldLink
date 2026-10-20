using System;
using Xunit;

namespace FieldLink.Transport.Framing.Tests;

public sealed class HeaderLengthFrameDecoderTests
{
    [Fact]
    public void Constructor_WithInvalidArguments_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderLengthFrameDecoder(0, 0, 2, true, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderLengthFrameDecoder(6, -1, 2, true, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderLengthFrameDecoder(6, 0, 3, true, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderLengthFrameDecoder(6, 5, 2, true, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderLengthFrameDecoder(6, 0, 2, true, 0, maximumFrameLength: 5));
    }

    [Fact]
    public void TryGetFrameLength_ModbusTcpMbapHeader_WhenIncompleteHeader_ReturnsFalse()
    {
        // Modbus TCP MBAP: 6바이트 헤더, 오프셋 4에 2바이트 빅엔디언 길이, 전체 길이 = 6 + 길이값
        var decoder = new HeaderLengthFrameDecoder(
            headerLength: 6,
            lengthFieldOffset: 4,
            lengthFieldByteCount: 2,
            isBigEndian: true,
            lengthAdjustment: 6,
            maximumFrameLength: 260);

        var buffer = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00 };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_ModbusTcpMbapHeader_WhenIncompletePayload_ReturnsFalse()
    {
        var decoder = new HeaderLengthFrameDecoder(
            headerLength: 6,
            lengthFieldOffset: 4,
            lengthFieldByteCount: 2,
            isBigEndian: true,
            lengthAdjustment: 6,
            maximumFrameLength: 260);

        // Header says remaining 6 bytes (total 12 bytes), but only 10 bytes received
        var buffer = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x6B };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.False(result);
        Assert.Equal(0, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_ModbusTcpMbapHeader_WhenComplete_ReturnsTrueAndTotalLength()
    {
        var decoder = new HeaderLengthFrameDecoder(
            headerLength: 6,
            lengthFieldOffset: 4,
            lengthFieldByteCount: 2,
            isBigEndian: true,
            lengthAdjustment: 6,
            maximumFrameLength: 260);

        // Total 12 bytes
        var buffer = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x6B, 0x00, 0x03 };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.True(result);
        Assert.Equal(12, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_LittleEndian_DecodesCorrectly()
    {
        // 4-byte header, length at offset 2, 2 bytes little-endian, total = 4 + length
        var decoder = new HeaderLengthFrameDecoder(
            headerLength: 4,
            lengthFieldOffset: 2,
            lengthFieldByteCount: 2,
            isBigEndian: false,
            lengthAdjustment: 4,
            maximumFrameLength: 100);

        // Length is 0x0005 (5 bytes payload -> total 9 bytes)
        var buffer = new byte[] { 0xAA, 0xBB, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength);

        Assert.True(result);
        Assert.Equal(9, frameLength);
    }

    [Fact]
    public void TryGetFrameLength_WhenCalculatedLengthExceedsMaximum_ThrowsInvalidFrame()
    {
        var decoder = new HeaderLengthFrameDecoder(
            headerLength: 4,
            lengthFieldOffset: 2,
            lengthFieldByteCount: 2,
            isBigEndian: true,
            lengthAdjustment: 4,
            maximumFrameLength: 50);

        // Length field indicates 100 -> total 104 > 50
        var buffer = new byte[] { 0x01, 0x02, 0x00, 0x64 };

        TransportException exception = Assert.Throws<TransportException>(
            () => decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out _));

        Assert.Equal(TransportFailure.InvalidFrame, exception.Failure);
    }
}
