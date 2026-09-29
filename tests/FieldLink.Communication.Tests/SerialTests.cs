using System.IO.Ports;
using FieldLink.Communication.Serial;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static Task SerialSettingsParseReferenceFormatAsync()
    {
        var settings = SerialPortSettings.Parse("COM5-19200-7-E-2");
        TestAssert.Equal("COM5", settings.PortName);
        TestAssert.Equal(19200, settings.BaudRate);
        TestAssert.Equal(7, settings.DataBits);
        TestAssert.Equal(Parity.Even, settings.Parity);
        TestAssert.Equal(StopBits.Two, settings.StopBits);
        var defaults = SerialPortSettings.Parse("COM3");
        TestAssert.Equal(9600, defaults.BaudRate);
        TestAssert.Equal(8, defaults.DataBits);
        TestAssert.Equal(StopBits.One, defaults.StopBits);
        var fractional = SerialPortSettings.Parse("COM7;38400;8;O;1.5");
        TestAssert.Equal(Parity.Odd, fractional.Parity);
        TestAssert.Equal(StopBits.OnePointFive, fractional.StopBits);
        return Task.CompletedTask;
    }

    internal static async Task SerialSettingsRejectInvalidConfigurationAsync()
    {
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(SerialPortSettings.Parse("COM1-0-8-N-1")));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(SerialPortSettings.Parse("COM1-9600-9-N-1")));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(SerialPortSettings.Parse("COM1-9600-8-X-1")));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(SerialPortSettings.Parse("COM1-9600")));
    }
}
