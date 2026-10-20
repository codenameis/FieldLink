using System;
using System.Text;
using Xunit;

namespace FieldLink.Plc.Conversion.Tests;

public sealed class PaddedStringFormatTests
{
    [Fact]
    public void GetStorageSize_WordAligned_RoundsUpToWordSize()
    {
        var format = new PaddedStringFormat(paddingByte: 0x00, wordSize: 2);

        Assert.Equal(2, format.GetStorageSize(1));
        Assert.Equal(2, format.GetStorageSize(2));
        Assert.Equal(4, format.GetStorageSize(3));
        Assert.Equal(10, format.GetStorageSize(10));
    }

    [Fact]
    public void Encode_PadsRemainingBytesWithNull()
    {
        var format = new PaddedStringFormat(paddingByte: 0x00, wordSize: 2);

        byte[] encoded = format.Encode("ABC", Encoding.ASCII, characterCapacity: 4);

        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x00 }, encoded);
    }

    [Fact]
    public void Decode_StopsAtPaddingByte()
    {
        var format = new PaddedStringFormat(paddingByte: 0x00, wordSize: 2);
        byte[] storage = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x00, 0x00 };

        string decoded = format.Decode(storage, Encoding.ASCII);

        Assert.Equal("Hello", decoded);
    }

    [Fact]
    public void Encode_ExceedingCapacity_ThrowsArgumentException()
    {
        var format = new PaddedStringFormat(paddingByte: 0x00, wordSize: 2);

        Assert.Throws<ArgumentException>(() => format.Encode("ToolongString", Encoding.ASCII, characterCapacity: 4));
    }
}
