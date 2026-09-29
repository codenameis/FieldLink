using System.Text;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron.Clients;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task FxLinksAndHostLinkSerialFramesAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        void Queue(string data) => port.Incoming.Enqueue(Encoding.ASCII.GetBytes(data));
        var fx = new MelsecFxLinksSerialClient(transport, new FxLinksFrameOptions { Station = 1 });
        Queue("\u000201FF1234ABCD\u000300");
        TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], (await fx.ReadAsync("D100", 2)).Content);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("\u000501FFWR0D0100022D"), port.Writes.Last());
        Queue("\u001501FF02"); var error = await fx.WriteAsync("M0", new[] { true });
        TestAssert.True(!error.IsSuccess); TestAssert.Equal(2, error.ErrorCode);
        Queue("\u000601FF"); TestAssert.True((await fx.StopPlcAsync()).IsSuccess);
        var cmode = new OmronHostLinkCModeSerialClient(transport);
        Queue("@00RD001234ABCD00*\r");
        TestAssert.Bytes([0x12, 0x34, 0xAB, 0xCD], (await cmode.ReadAsync("D100", 2)).Content);
        TestAssert.Equal("@00RD0100000255*\r", Encoding.ASCII.GetString(port.Writes.Last()));
        var host = new OmronHostLinkSerialClient(transport);
        Queue("@00FA0040000000010100001234ABCD00*\r");
        var read = await host.ReadAsync("D100", 2);
        TestAssert.True(read.IsSuccess, read.Message); TestAssert.Bytes([0x12, 0x34, 0xAB, 0xCD], read.Content);
        Queue("@00FA00400000000102210100*\r");
        var denied = await host.WriteAsync("D100", new byte[] { 1, 2 });
        TestAssert.True(!denied.IsSuccess); TestAssert.Equal(0x2101, denied.ErrorCode);
    }
}
