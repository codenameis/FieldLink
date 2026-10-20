using System;
using System.Linq;
using FieldLink.Plc.Protocols.Siemens.S7Comm;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Siemens.S7Comm;

public sealed class S7CommRequestBuilderTests
{
    private const int TpktAndCotpDataHeaderLength = 7;

    [Fact]
    public void ConnectionRequest_MatchesVectorS1()
    {
        byte[] frame = S7CommRequestBuilder.BuildConnectionRequest(new S7CommOptions(rack: 0, slot: 1));

        Assert.Equal(FromHex("03 00 00 16 11 E0 00 00 00 01 00 C1 02 01 00 C2 02 01 01 C0 01 0A"), frame);
    }

    [Fact]
    public void ConnectionRequest_RackAndSlotChangeRemoteTsap()
    {
        byte[] frame = S7CommRequestBuilder.BuildConnectionRequest(new S7CommOptions(rack: 1, slot: 2));

        Assert.Equal(new byte[] { 0x01, 0x22 }, new[] { frame[17], frame[18] });
    }

    [Fact]
    public void SetupCommunication_MatchesVectorS2()
    {
        byte[] frame = S7CommRequestBuilder.BuildSetupCommunicationRequest(new S7CommOptions(), 1);

        Assert.Equal(FromHex("03 00 00 19 02 F0 80 32 01 00 00 00 01 00 08 00 00 F0 00 00 01 00 01 01 E0"), frame);
    }

    [Fact]
    public void ReadRequest_MatchesVectorS3()
    {
        byte[] frame = S7CommRequestBuilder.BuildReadRequest(2, S7CommAddress.Parse("DB1.DBW10"), 2);

        Assert.Equal(
            FromHex("03 00 00 1F 02 F0 80 32 01 00 00 00 02 00 0E 00 00 04 01 12 0A 10 02 00 02 00 01 84 00 00 50"),
            frame);
    }

    [Fact]
    public void ReadRequest_Bit_MatchesVectorS8()
    {
        byte[] frame = S7CommRequestBuilder.BuildReadRequest(4, S7CommAddress.Parse("M10.3"), 1);

        Assert.Equal(FromHex("32 01 00 00 00 04 00 0E 00 00 04 01 12 0A 10 01 00 01 00 00 83 00 00 53"), SkipTransportHeaders(frame));
    }

    [Fact]
    public void WriteRequest_MatchesVectorS5()
    {
        byte[] frame = S7CommRequestBuilder.BuildWriteRequest(3, S7CommAddress.Parse("DB1.DBW10"), new byte[] { 0x12, 0x34 });

        Assert.Equal(
            FromHex("03 00 00 25 02 F0 80 32 01 00 00 00 03 00 0E 00 06 05 01 12 0A 10 02 00 02 00 01 84 00 00 50 00 04 00 10 12 34"),
            frame);
    }

    [Fact]
    public void WriteRequest_Bit_UsesBitTransportAndNormalizesValue()
    {
        byte[] frame = S7CommRequestBuilder.BuildWriteRequest(5, S7CommAddress.Parse("M10.3"), new byte[] { 0x07 });

        Assert.Equal(
            FromHex("32 01 00 00 00 05 00 0E 00 05 05 01 12 0A 10 01 00 01 00 00 83 00 00 53 00 03 00 01 01"),
            SkipTransportHeaders(frame));
    }

    [Fact]
    public void ReadRequest_MemoryAreaAndInputAreaCodes()
    {
        byte[] memory = S7CommRequestBuilder.BuildReadRequest(1, S7CommAddress.Parse("MB0"), 1);
        byte[] input = S7CommRequestBuilder.BuildReadRequest(1, S7CommAddress.Parse("IB0"), 1);
        byte[] output = S7CommRequestBuilder.BuildReadRequest(1, S7CommAddress.Parse("QB0"), 1);

        Assert.Equal(new byte[] { 0x83, 0x81, 0x82 }, new[] { memory[27], input[27], output[27] });
    }

    [Fact]
    public void ReadRequest_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7CommRequestBuilder.BuildReadRequest(1, S7CommAddress.Parse("MB0"), 0));
        Assert.Throws<ArgumentException>(() => S7CommRequestBuilder.BuildReadRequest(1, S7CommAddress.Parse("M0.0"), 2));
        Assert.Throws<ArgumentNullException>(() => S7CommRequestBuilder.BuildReadRequest(1, null!, 1));
    }

    [Fact]
    public void WriteRequest_InvalidInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => S7CommRequestBuilder.BuildWriteRequest(1, S7CommAddress.Parse("MB0"), Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => S7CommRequestBuilder.BuildWriteRequest(1, S7CommAddress.Parse("M0.0"), new byte[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7CommRequestBuilder.BuildWriteRequest(1, S7CommAddress.Parse("MB0"), new byte[8192]));
    }

    [Theory]
    [InlineData(-1, 1, 480)]
    [InlineData(8, 1, 480)]
    [InlineData(0, -1, 480)]
    [InlineData(0, 32, 480)]
    [InlineData(0, 1, 63)]
    [InlineData(0, 1, 65536)]
    public void Options_OutOfRange_Throws(int rack, int slot, int requestedPduLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new S7CommOptions(rack, slot, requestedPduLength));
    }

    private static byte[] SkipTransportHeaders(byte[] frame) => frame.Skip(TpktAndCotpDataHeaderLength).ToArray();

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
