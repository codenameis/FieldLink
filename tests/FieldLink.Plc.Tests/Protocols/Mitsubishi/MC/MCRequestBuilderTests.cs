using System;
using System.Text;
using FieldLink.Plc.Protocols.Mitsubishi.MC;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Mitsubishi.MC;

public sealed class MCRequestBuilderTests
{
    private static readonly MCOptions QL3E = new(MCFrameType.Frame3E, MCSeries.QL);
    private static readonly MCOptions Iqr3E = new(MCFrameType.Frame3E, MCSeries.Iqr);
    private static readonly MCOptions QL4E = new(MCFrameType.Frame4E, MCSeries.QL);
    private static readonly MCOptions Iqr4E = new(MCFrameType.Frame4E, MCSeries.Iqr);

    [Fact]
    public void Binary_3E_QL_ReadWord_MatchesVectorM1()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("D100"), 1);

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0C 00 10 00 01 04 00 00 64 00 00 A8 01 00"), frame);
    }

    [Fact]
    public void Binary_3E_Iqr_ReadWord_MatchesVectorM2()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildReadRequest(Iqr3E, 0, MCAddress.Parse("D10"), 1);

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0E 00 10 00 01 04 02 00 0A 00 00 00 A8 00 01 00"), frame);
    }

    [Fact]
    public void Binary_4E_QL_ReadWord_MatchesVectorM3()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildReadRequest(QL4E, 0x1234, MCAddress.Parse("D100"), 1);

        Assert.Equal(FromHex("54 00 34 12 00 00 00 FF FF 03 00 0C 00 10 00 01 04 00 00 64 00 00 A8 01 00"), frame);
    }

    [Fact]
    public void Binary_3E_QL_WriteBits_MatchesVectorM6()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("M10"), 3, new byte[] { 1, 0, 1 });

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0E 00 10 00 01 14 01 00 0A 00 00 90 03 00 10 10"), frame);
    }

    [Fact]
    public void Binary_3E_QL_WriteWords_PlacesLittleEndianData()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("D100"), 1, new byte[] { 0x34, 0x12 });

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0E 00 10 00 01 14 00 00 64 00 00 A8 01 00 34 12"), frame);
    }

    [Fact]
    public void Binary_HexDevice_UsesDeviceNumberValue()
    {
        byte[] frame = MCBinaryRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("X1F"), 16);

        Assert.Equal(FromHex("50 00 00 FF FF 03 00 0C 00 10 00 01 04 01 00 1F 00 00 9C 10 00"), frame);
    }

    [Fact]
    public void Ascii_3E_QL_ReadWord_MatchesVectorM4()
    {
        byte[] frame = MCAsciiRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("D100"), 1);

        Assert.Equal("500000FF03FF000018001004010000D*0001000001", Encoding.ASCII.GetString(frame));
    }

    [Fact]
    public void Ascii_4E_Iqr_ReadWord_MatchesVectorM5()
    {
        byte[] frame = MCAsciiRequestBuilder.BuildReadRequest(Iqr4E, 0x1234, MCAddress.Parse("D100"), 1);

        Assert.Equal("54001234000000FF03FF00001C001004010002D***000001000001", Encoding.ASCII.GetString(frame));
    }

    [Fact]
    public void Ascii_3E_QL_WriteWords_PutsHighByteFirst()
    {
        byte[] frame = MCAsciiRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("D100"), 2, new byte[] { 0x95, 0x19, 0x02, 0x12 });

        Assert.EndsWith("19951202", Encoding.ASCII.GetString(frame), StringComparison.Ordinal);
    }

    [Fact]
    public void Ascii_3E_QL_WriteBits_UsesOneCharacterPerBit()
    {
        byte[] frame = MCAsciiRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("M10"), 3, new byte[] { 1, 0, 1 });

        Assert.EndsWith("101", Encoding.ASCII.GetString(frame), StringComparison.Ordinal);
    }

    [Fact]
    public void Ascii_HexDevice_FormatsNumberInHex()
    {
        byte[] frame = MCAsciiRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("X1F"), 16);

        Assert.Contains("X*00001F0010", Encoding.ASCII.GetString(frame), StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ZeroPointCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MCBinaryRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("D0"), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MCAsciiRequestBuilder.BuildReadRequest(QL3E, 0, MCAddress.Parse("D0"), 0));
    }

    [Fact]
    public void Build_QLDeviceNumberBeyond24Bit_Throws()
    {
        var tooLarge = MCAddress.Parse("D16777216");

        Assert.Throws<ArgumentOutOfRangeException>(() => MCBinaryRequestBuilder.BuildReadRequest(QL3E, 0, tooLarge, 1));
        MCBinaryRequestBuilder.BuildReadRequest(Iqr3E, 0, tooLarge, 1);
    }

    [Fact]
    public void BuildWrite_DataLengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => MCBinaryRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("D0"), 2, new byte[2]));
        Assert.Throws<ArgumentException>(() => MCAsciiRequestBuilder.BuildWriteRequest(QL3E, 0, MCAddress.Parse("M0"), 3, new byte[2]));
    }

    [Fact]
    public void Options_InvalidLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MCOptions(maxWordPoints: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MCOptions(maxBitPoints: 0));
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
