using System;
using System.IO.Ports;
using FieldLink.Transport.Serial;
using Xunit;

namespace FieldLink.Transport.Tests.Serial;

public sealed class SerialTransportOptionsTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesProperties()
    {
        var options = new SerialTransportOptions("COM3", 115200, Parity.None, 8, StopBits.One);

        Assert.Equal("COM3", options.PortName);
        Assert.Equal(115200, options.BaudRate);
        Assert.Equal(Parity.None, options.Parity);
        Assert.Equal(8, options.DataBits);
        Assert.Equal(StopBits.One, options.StopBits);
    }

    [Fact]
    public void Constructor_WithInvalidPortName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SerialTransportOptions(""));
        Assert.Throws<ArgumentException>(() => new SerialTransportOptions("   "));
        Assert.Throws<ArgumentNullException>(() => new SerialTransportOptions(null!));
    }

    [Fact]
    public void Constructor_WithInvalidBaudRate_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialTransportOptions("COM1", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialTransportOptions("COM1", -9600));
    }

    [Fact]
    public void Constructor_WithInvalidDataBits_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialTransportOptions("COM1", 9600, dataBits: 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialTransportOptions("COM1", 9600, dataBits: 9));
    }
}
