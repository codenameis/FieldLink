using System;
using FieldLink.Plc.Protocols.Siemens.S7Comm;
using FieldLink.Transport;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Siemens.S7Comm;

public sealed class S7CommResponseParserTests
{
    private static readonly S7CommAddress WordAddress = S7CommAddress.Parse("DB1.DBW10");
    private static readonly S7CommAddress BitAddress = S7CommAddress.Parse("M10.3");

    [Fact]
    public void ReadResponse_MatchesVectorS4()
    {
        byte[] frame = Wrap("32 03 00 00 00 02 00 02 00 06 00 00 04 01 FF 04 00 10 12 34");

        byte[] values = S7CommResponseParser.ParseReadResponse(frame, 2, WordAddress, 2);

        Assert.Equal(new byte[] { 0x12, 0x34 }, values);
    }

    [Fact]
    public void ReadResponse_Bit_ReturnsZeroOrOne()
    {
        byte[] frame = Wrap("32 03 00 00 00 04 00 02 00 05 00 00 04 01 FF 03 00 01 01");

        byte[] values = S7CommResponseParser.ParseReadResponse(frame, 4, BitAddress, 1);

        Assert.Equal(new byte[] { 1 }, values);
    }

    [Fact]
    public void ReadResponse_ErrorReturnCode_MatchesVectorS7()
    {
        byte[] frame = Wrap("32 03 00 00 00 02 00 02 00 04 00 00 04 01 05 00 00 00");

        var exception = Assert.Throws<PlcDeviceException>(() => S7CommResponseParser.ParseReadResponse(frame, 2, WordAddress, 2));

        Assert.Equal(0x05, exception.DeviceErrorCode);
    }

    [Fact]
    public void ReadResponse_WrongDataSize_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 02 00 02 00 06 00 00 04 01 FF 04 00 10 12 34");

        AssertInvalidResponse(() => S7CommResponseParser.ParseReadResponse(frame, 2, WordAddress, 3));
    }

    [Fact]
    public void ReadResponse_WrongDeclaredBitLength_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 02 00 02 00 06 00 00 04 01 FF 04 00 08 12 34");

        AssertInvalidResponse(() => S7CommResponseParser.ParseReadResponse(frame, 2, WordAddress, 2));
    }

    [Fact]
    public void WriteResponse_MatchesVectorS6()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");

        S7CommResponseParser.ParseWriteResponse(frame, 3);
    }

    [Fact]
    public void WriteResponse_ErrorReturnCode_ThrowsDeviceException()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 0A");

        var exception = Assert.Throws<PlcDeviceException>(() => S7CommResponseParser.ParseWriteResponse(frame, 3));

        Assert.Equal(0x0A, exception.DeviceErrorCode);
    }

    [Fact]
    public void HeaderError_ThrowsDeviceExceptionWithCombinedCode()
    {
        byte[] frame = Wrap("32 02 00 00 00 03 00 00 00 00 81 04");

        var exception = Assert.Throws<PlcDeviceException>(() => S7CommResponseParser.ParseWriteResponse(frame, 3));

        Assert.Equal(0x8104, exception.DeviceErrorCode);
    }

    [Fact]
    public void WrongPduReference_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 9));
    }

    [Fact]
    public void ReadPduReference_ReadsBigEndianValue()
    {
        byte[] frame = Wrap("32 03 00 00 01 02 00 02 00 01 00 00 05 01 FF");

        Assert.Equal(0x0102, S7CommResponseParser.ReadPduReference(frame));
    }

    [Fact]
    public void WrongProtocolId_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("33 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 3));
    }

    [Fact]
    public void RequestMessageType_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 01 00 00 00 03 00 02 00 01 00 00 05 01 FF");

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 3));
    }

    [Fact]
    public void LengthSumMismatch_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 05 00 00 05 01 FF");

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 3));
    }

    [Fact]
    public void TpktLengthMismatch_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");
        frame[3] += 1;

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 3));
    }

    [Fact]
    public void WrongCotpHeader_ThrowsInvalidResponse()
    {
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");
        frame[5] = 0xE0;

        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(frame, 3));
    }

    [Fact]
    public void TooShortFrame_ThrowsInvalidResponse()
    {
        AssertInvalidResponse(() => S7CommResponseParser.ParseWriteResponse(new byte[] { 0x03, 0x00, 0x00, 0x05, 0x00 }, 1));
    }

    [Theory]
    [InlineData("32 03 00 00 00 01 00 08 00 00 00 00 F0 00 00 01 00 01 01 E0", 480)]
    [InlineData("32 03 00 00 00 01 00 08 00 00 00 00 F0 00 00 01 00 01 00 F0", 240)]
    public void SetupCommunication_ReturnsNegotiatedPduLength(string s7Hex, int expectedLength)
    {
        Assert.Equal(expectedLength, S7CommResponseParser.ParseSetupCommunication(Wrap(s7Hex), 1));
    }

    [Fact]
    public void SetupCommunication_TooSmallPduLength_ThrowsConnectionFailed()
    {
        byte[] frame = Wrap("32 03 00 00 00 01 00 08 00 00 00 00 F0 00 00 01 00 01 00 0A");

        var exception = Assert.Throws<PlcException>(() => S7CommResponseParser.ParseSetupCommunication(frame, 1));

        Assert.Equal(PlcErrorKind.ConnectionFailed, exception.Kind);
    }

    [Fact]
    public void ConnectionConfirm_Valid_DoesNotThrow()
    {
        S7CommResponseParser.ParseConnectionConfirm(FromHex("03 00 00 16 11 D0 00 01 00 01 00 C0 01 0A C1 02 01 00 C2 02 01 01"));
    }

    [Fact]
    public void ConnectionConfirm_WrongPduType_ThrowsConnectionFailed()
    {
        var exception = Assert.Throws<PlcException>(() =>
            S7CommResponseParser.ParseConnectionConfirm(FromHex("03 00 00 16 11 E0 00 01 00 01 00 C0 01 0A C1 02 01 00 C2 02 01 01")));

        Assert.Equal(PlcErrorKind.ConnectionFailed, exception.Kind);
    }

    [Fact]
    public void Decoder_WaitsForWholeTpktFrame()
    {
        var decoder = new S7CommFrameDecoder();
        byte[] frame = Wrap("32 03 00 00 00 03 00 02 00 01 00 00 05 01 FF");

        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, 3), out _));
        Assert.False(decoder.TryGetFrameLength(new ArraySegment<byte>(frame, 0, frame.Length - 1), out _));
        Assert.True(decoder.TryGetFrameLength(new ArraySegment<byte>(frame), out int frameLength));
        Assert.Equal(frame.Length, frameLength);
    }

    [Fact]
    public void Decoder_TpktLengthSmallerThanHeader_ThrowsTransportException()
    {
        var decoder = new S7CommFrameDecoder();

        Assert.Throws<TransportException>(() => decoder.TryGetFrameLength(new ArraySegment<byte>(FromHex("03 00 00 02")), out _));
    }

    private static void AssertInvalidResponse(Action action)
    {
        var exception = Assert.Throws<PlcException>(action);

        Assert.Equal(PlcErrorKind.InvalidResponse, exception.Kind);
    }

    private static byte[] Wrap(string s7Hex)
    {
        byte[] s7 = FromHex(s7Hex);
        int totalLength = 7 + s7.Length;
        byte[] frame = new byte[totalLength];
        FromHex("03 00 00 00 02 F0 80").CopyTo(frame, 0);
        frame[2] = (byte)(totalLength >> 8);
        frame[3] = (byte)totalLength;
        s7.CopyTo(frame, 7);
        return frame;
    }

    private static byte[] FromHex(string spacedHex) => Convert.FromHexString(spacedHex.Replace(" ", string.Empty));
}
