using System.Text;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Keyence.Clients;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task KeyenceSerialReconnectAndScannerResponsesAsync()
    {
        var first = new FakeSerialPort(); var second = new FakeSerialPort();
        var ports = new Queue<FakeSerialPort>([first, second]);
        using var transport = new SerialClient(new SerialPortSettings("virtual"), new FixedLengthFrame(1), portFactory: _ => ports.Dequeue());
        var nano = new KeyenceNanoSerialClient(transport, 3, true);
        await transport.OpenAsync();
        first.Incoming.Enqueue(Encoding.ASCII.GetBytes("CC\r\n4660 43981\r\n"));
        TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], (await nano.ReadAsync("DM100", 2)).Content);
        TestAssert.Equal("CR 03\r", Encoding.ASCII.GetString(first.Writes.First()));
        TestAssert.Equal("RDS DM100 2\r", Encoding.ASCII.GetString(first.Writes.Last()));
        first.Incoming.Enqueue(Encoding.ASCII.GetBytes("CF\r\n"));
        TestAssert.True((await nano.DisconnectAsync()).IsSuccess);
        TestAssert.Equal("CQ\r", Encoding.ASCII.GetString(first.Writes.Last()));
        transport.Close(); await transport.OpenAsync();
        second.Incoming.Enqueue(Encoding.ASCII.GetBytes("CC\r\n7\r\n"));
        TestAssert.Bytes([7, 0], (await nano.ReadAsync("DM100", 1)).Content);
        TestAssert.Equal("CR 03\r", Encoding.ASCII.GetString(second.Writes.First()));
        var scanner = new Sr2000SerialClient(transport);
        second.Incoming.Enqueue(Encoding.ASCII.GetBytes("OK,INCHK,ON\r"));
        TestAssert.True((await scanner.CheckInputAsync(1)).Content);
        TestAssert.Equal("INCHK,1\r", Encoding.ASCII.GetString(second.Writes.Last()));
        second.Incoming.Enqueue(Encoding.ASCII.GetBytes("ER,LON,02\r"));
        TestAssert.True(!(await scanner.ReadBarcodeAsync()).IsSuccess);
    }
}
