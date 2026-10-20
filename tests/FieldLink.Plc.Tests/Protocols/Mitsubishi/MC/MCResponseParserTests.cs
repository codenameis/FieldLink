using System;
using System.Text;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using FieldLink.Transport;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Mitsubishi.MC;

public sealed class MCResponseParserTests
{
    private static readonly MCOptions QL3E = new(MCFrameType.Frame3E, MCSeries.QL);
    private static readonly MCOptions Iqr4E = new(MCFrameType.Frame4E, MCSeries.Iqr);
    private static readonly MCOptions QL4E = new(MCFrameType.Frame4E, MCSeries.QL);

    [Fact]
    public void Binary_3E_ReadWord_MatchesVectorM1()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 04 00 00 00 34 12");

        byte[] data = MCBinaryResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("D100"), 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void Binary_4E_ReadWord_MatchesVectorM3()
    {
        byte[] frame = FromHex("D4 00 34 12 00 00 00 FF FF 03 00 04 00 00 00 34 12");

        byte[] data = MCBinaryResponseParser.ParseRead(QL4E, 0x1234, frame, MCAddress.Parse("D100"), 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
        Assert.Equal(0x1234, MCBinaryResponseParser.ReadSerialNumber(frame));
    }

    [Fact]
    public void Binary_ReadBits_UnpacksNibbles()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 04 00 00 00 10 10");

        byte[] bits = MCBinaryResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("M10"), 3);

        Assert.Equal(new byte[] { 1, 0, 1 }, bits);
    }

    [Fact]
    public void Binary_Write_NormalResponse_Succeeds()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 02 00 00 00");

        MCBinaryResponseParser.ParseWrite(QL3E, 0, frame);
    }

    [Fact]
    public void Binary_NonZeroEndCode_ThrowsDeviceException()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 02 00 56 C0");

        var exception = Assert.Throws<PlcDeviceException>(() => MCBinaryResponseParser.ParseWrite(QL3E, 0, frame));

        Assert.Equal(0xC056, exception.DeviceErrorCode);
    }

    [Fact]
    public void Binary_WrongSerialNumber_ThrowsInvalidResponse()
    {
        byte[] frame = FromHex("D4 00 34 12 00 00 00 FF FF 03 00 02 00 00 00");

        var exception = Assert.Throws<PlcException>(() => MCBinaryResponseParser.ParseWrite(QL4E, 0x9999, frame));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void Binary_LengthFieldMismatch_ThrowsInvalidResponse()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 09 00 00 00 34 12");

        var exception = Assert.Throws<PlcException>(() => MCBinaryResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("D0"), 1));

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void Binary_WrongSubheader_ThrowsInvalidResponse()
    {
        byte[] frame = FromHex("50 00 00 FF FF 03 00 02 00 00 00");

        Assert.Throws<PlcException>(() => MCBinaryResponseParser.ParseWrite(QL3E, 0, frame));
    }

    [Fact]
    public void Binary_WrongDataSize_ThrowsInvalidResponse()
    {
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 04 00 00 00 34 12");

        Assert.Throws<PlcException>(() => MCBinaryResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("D0"), 2));
    }

    [Fact]
    public void Ascii_3E_ReadWord_MatchesVectorM4()
    {
        byte[] frame = Ascii("D00000FF03FF00000800001234");

        byte[] data = MCAsciiResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("D100"), 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
    }

    [Fact]
    public void Ascii_4E_ReadWord_MatchesVectorM5()
    {
        byte[] frame = Ascii("D4001234000000FF03FF00000800001234");

        byte[] data = MCAsciiResponseParser.ParseRead(Iqr4E, 0x1234, frame, MCAddress.Parse("D100"), 1);

        Assert.Equal(new byte[] { 0x34, 0x12 }, data);
        Assert.Equal(0x1234, MCAsciiResponseParser.ReadSerialNumber(frame));
    }

    [Fact]
    public void Ascii_ReadBits_ReadsOneCharacterPerBit()
    {
        byte[] frame = Ascii("D00000FF03FF0000070000101");

        byte[] bits = MCAsciiResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("M10"), 3);

        Assert.Equal(new byte[] { 1, 0, 1 }, bits);
    }

    [Fact]
    public void Ascii_Write_NormalResponse_Succeeds()
    {
        byte[] frame = Ascii("D00000FF03FF00000400" + "00");

        MCAsciiResponseParser.ParseWrite(QL3E, 0, frame);
    }

    [Fact]
    public void Ascii_NonZeroEndCode_ThrowsDeviceException()
    {
        byte[] frame = Ascii("D00000FF03FF000004C056");

        var exception = Assert.Throws<PlcDeviceException>(() => MCAsciiResponseParser.ParseWrite(QL3E, 0, frame));

        Assert.Equal(0xC056, exception.DeviceErrorCode);
    }

    [Fact]
    public void Ascii_NonHexCharacterInWord_ThrowsInvalidResponse()
    {
        byte[] frame = Ascii("D00000FF03FF00000800001G34");

        Assert.Throws<PlcException>(() => MCAsciiResponseParser.ParseRead(QL3E, 0, frame, MCAddress.Parse("D0"), 1));
    }

    [Fact]
    public void BinaryDecoder_3E_WaitsForWholeFrame()
    {
        var decoder = new MCBinaryFrameDecoder(MCFrameType.Frame3E);
        byte[] frame = FromHex("D0 00 00 FF FF 03 00 04 00 00 00 34 12");

        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, 8), out _));
        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, frame.Length - 1), out _));
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out int frameLength));
        Assert.Equal(frame.Length, frameLength);
    }

    [Fact]
    public void BinaryDecoder_4E_UsesThirteenByteHeader()
    {
        var decoder = new MCBinaryFrameDecoder(MCFrameType.Frame4E);
        byte[] frame = FromHex("D4 00 34 12 00 00 00 FF FF 03 00 04 00 00 00 34 12");

        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out int frameLength));
        Assert.Equal(frame.Length, frameLength);
    }

    [Fact]
    public void AsciiDecoder_3E_WaitsForWholeFrame()
    {
        var decoder = new MCAsciiFrameDecoder(MCFrameType.Frame3E);
        byte[] frame = Ascii("D00000FF03FF00000800001234");

        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, 17), out _));
        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, frame.Length - 1), out _));
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out int frameLength));
        Assert.Equal(frame.Length, frameLength);
    }

    [Fact]
    public void AsciiDecoder_4E_UsesTwentySixCharacterHeader()
    {
        var decoder = new MCAsciiFrameDecoder(MCFrameType.Frame4E);
        byte[] frame = Ascii("D4001234000000FF03FF00000800001234");

        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out int frameLength));
        Assert.Equal(frame.Length, frameLength);
    }

    [Fact]
    public void AsciiDecoder_InvalidLengthField_ThrowsTransportException()
    {
        var decoder = new MCAsciiFrameDecoder(MCFrameType.Frame3E);
        byte[] frame = Ascii("D00000FF03FF00ZZZZ0000");

        Assert.Throws<TransportException>(() => decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out _));
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
}
