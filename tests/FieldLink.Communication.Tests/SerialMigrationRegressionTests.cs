using FieldLink.PlcDrivers.Modbus.Clients;
using System.Text;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Toledo.Clients;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task MigrationModbusWordBitWriteLimitBeforeIoAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var client = new FieldLink.PlcDrivers.Modbus.Clients.ModbusSerialClient(transport, timeout: TimeSpan.FromMilliseconds(60));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WriteAsync("100.15", new bool[1968]));
        TestAssert.Equal(0, port.Writes.Count);
        TestAssert.Equal(ClientState.Open, transport.State);
    }
    internal static async Task MigrationFxLinksWordBitAddressAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var fx = new MelsecFxLinksSerialClient(transport);
        port.Incoming.Enqueue(Encoding.ASCII.GetBytes("\u000207FF80010003\u000300"));
        var result = await fx.ReadBoolAsync("s=7;D100.15", 3);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.True(result.Content.SequenceEqual(new[] { true, true, true }));
        TestAssert.True(Encoding.ASCII.GetString(port.Writes.Last()).Contains("07FFWR0D010002"));
    }
    internal static async Task MigrationA3cInvalidInputKeepsPortOpenAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var client = new MelsecA3CSerialClient(transport, new A3CFrameOptions { EnableWriteBitToWordRegister = true });
        try { await client.ReadBoolAsync("D100.wrong", 1); } catch (Exception) { }
        TestAssert.Equal(ClientState.Open, transport.State);
        try { await client.WriteAsync("D100.0", new bool[1048576]); } catch (Exception) { }
        TestAssert.Equal(ClientState.Open, transport.State);
        TestAssert.Equal(0, port.Writes.Count);
    }
    internal static async Task MigrationToledoDefaultHasNoCheckByteAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var scale = new ToledoSerialClient(transport, timeout: TimeSpan.FromMilliseconds(100));
        port.Incoming.Enqueue(Encoding.ASCII.GetBytes("\u0002\u0004\0\0" + "001234" + "000100" + "\r"));
        TestAssert.Equal(12.34f, (await scale.ReceiveAsync()).Weight);
    }
    internal static async Task MigrationFxBaudNegotiationAllowsThreeEnquiriesAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var fx = new MelsecFxSerialClient(transport);
        port.Incoming.Enqueue([0x15, 0x15, 6, 6]);
        TestAssert.True((await fx.ChangeDeviceBaudRateAsync(19200)).IsSuccess);
        TestAssert.Equal(4, port.Writes.Count);
        TestAssert.True(port.Writes.Take(3).All(p => p.SequenceEqual(new byte[] { 5 })));
    }
}
