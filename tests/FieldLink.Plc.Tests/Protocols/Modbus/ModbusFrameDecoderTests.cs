using System;
using System.Text;
using FieldLink.Plc.Conversion;
using FieldLink.Transport;
using Xunit;

namespace FieldLink.Plc.Protocols.Modbus.Tests;

public sealed class ModbusFrameDecoderTests
{
    // 출처: docs/specs/modbus.md §8-2 "F1" TCP 응답 디코딩 (15바이트)
    [Fact]
    public void ModbusTcpFrameDecoder_VectorF1_ReturnsFrameLength()
    {
        var decoder = new ModbusTcpFrameDecoder();
        byte[] adu = AsciiHex.GetBytes("000100000009010306022B00000064");

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out int frameLength);

        Assert.True(result);
        Assert.Equal(15, frameLength);
    }

    // 출처: docs/specs/modbus.md §8-2 "F2" RTU 읽기 응답 디코딩 (11바이트)
    [Fact]
    public void ModbusRtuFrameDecoder_VectorF2_ReturnsFrameLength()
    {
        var decoder = new ModbusRtuFrameDecoder();
        byte[] adu = AsciiHex.GetBytes("010306022B00000064057A");

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out int frameLength);

        Assert.True(result);
        Assert.Equal(11, frameLength);
    }

    // 출처: docs/specs/modbus.md §8-2 "F7" RTU 예외 응답 디코딩 (5바이트)
    [Fact]
    public void ModbusRtuFrameDecoder_VectorF7_ReturnsExceptionLength()
    {
        var decoder = new ModbusRtuFrameDecoder();
        byte[] adu = AsciiHex.GetBytes("018102C191");

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out int frameLength);

        Assert.True(result);
        Assert.Equal(5, frameLength);
    }

    // 출처: docs/specs/modbus.md §8-2 "F8" ASCII 응답 디코딩
    [Fact]
    public void ModbusAsciiFrameDecoder_VectorF8_ReturnsFrameLength()
    {
        var decoder = new ModbusAsciiFrameDecoder();
        byte[] adu = Encoding.ASCII.GetBytes(":0103006B00038E\r\n");

        bool result = decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out int frameLength);

        Assert.True(result);
        Assert.Equal(17, frameLength);
    }

    [Fact]
    public void ModbusTcpFrameDecoder_InvalidProtocolId_ThrowsTransportException()
    {
        var decoder = new ModbusTcpFrameDecoder();
        byte[] adu = AsciiHex.GetBytes("000100010009010306022B00000064");

        Assert.Throws<TransportException>(() => decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ModbusTcpFrameDecoder_LengthFieldBelowMinimum_ThrowsTransportException(int lengthField)
    {
        var decoder = new ModbusTcpFrameDecoder();
        byte[] adu = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, (byte)lengthField, 0x01, 0x03, 0x00 };

        Assert.Throws<TransportException>(() => decoder.TryGetFrameLength(new ArraySegment<byte>(adu), out _));
    }
}
