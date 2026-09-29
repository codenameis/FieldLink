using System.Text;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.AllenBradley.Clients;
using FieldLink.PlcDrivers.IDCard.Clients;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.PlcDrivers.Siemens.Clients;
using FieldLink.PlcDrivers.Toledo.Clients;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task Df1SamFxAndPassiveToledoSerialAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var df1 = new Df1SerialClient(transport);
        port.Incoming.Enqueue(TestBytes.FromHexString("100200004F000000341210030000"));
        TestAssert.Bytes([0x34, 0x12], (await df1.ReadAsync("N7:0", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("100100100200000F000000A20207890000"), TestBytes.Slice(port.Writes.Last(), 0, 17));
        var sam = new SamSerialClient(transport);
        port.Incoming.Enqueue(TestBytes.FromHexString("AAAAAA9669000400009F9B"));
        TestAssert.True((await sam.SearchCardAsync()).IsSuccess);
        TestAssert.Bytes(TestBytes.FromHexString("AAAAAA96690003200122"), port.Writes.Last());
        port.Incoming.Enqueue(TestBytes.FromHexString("AAAAAA9669000400008084"));
        TestAssert.True(!(await sam.SelectCardAsync()).IsSuccess);
        var fx = new MelsecFxSerialClient(transport);
        port.Incoming.Enqueue(Encoding.ASCII.GetBytes("\u00023412\u0003CD"));
        TestAssert.Bytes([0x34, 0x12], (await fx.ReadAsync("D100", 1)).Content);
        port.Incoming.Enqueue([6, 6, 6]);
        TestAssert.True((await fx.ActivatePlcAsync()).IsSuccess);
        TestAssert.Bytes(TestBytes.FromHexString("0230304530323032033643"), port.Writes.Last());
        port.Incoming.Enqueue([6, 6]);
        TestAssert.True((await fx.ChangeDeviceBaudRateAsync(19200)).IsSuccess);
        TestAssert.Bytes(TestBytes.FromHexString("024131033735"), port.Writes.Last());
        int writes = port.Writes.Count;
        var scale = new ToledoSerialClient(transport, false);
        byte[] weight = Encoding.ASCII.GetBytes("\u0002\u0004\0\0" + "001234" + "000100" + "\r");
        port.Incoming.Enqueue([..TestBytes.Slice(weight, 0, 7)]); port.Incoming.Enqueue([..TestBytes.Slice(weight, 7), ..weight]);
        TestAssert.Equal(12.34f, (await scale.ReceiveAsync()).Weight);
        TestAssert.Equal(1f, (await scale.ReceiveAsync()).Tare);
        TestAssert.Equal(writes, port.Writes.Count);
    }

    internal static async Task SiemensSerialTwoStageExchangeAndMpiReconnectAsync()
    {
        var first = new FakeSerialPort(); var second = new FakeSerialPort();
        var ports = new Queue<FakeSerialPort>([first, second]);
        using var transport = new SerialClient(new SerialPortSettings("virtual"), new FixedLengthFrame(1), portFactory: _ => ports.Dequeue());
        await transport.OpenAsync();
        var ppi = new SiemensPpiSerialClient(transport);
        byte[] ppiResponse = new byte[29]; ppiResponse[0] = 0x68; ppiResponse[1] = ppiResponse[2] = 23; ppiResponse[3] = 0x68;
        ppiResponse[21] = 0xFF; ppiResponse[22] = 4; ppiResponse[24] = 16; ppiResponse[25] = 0x12; ppiResponse[26] = 0x34; ppiResponse[28] = 0x16;
        first.Incoming.Enqueue([0xE5, ..ppiResponse]);
        TestAssert.Bytes([0x12, 0x34], (await ppi.ReadAsync("M100", 2)).Content);
        // 참조 GetExecuteConfirm: 10, 국번, 00, 5C, (국번+5C), 16.
        TestAssert.Bytes(TestBytes.FromHexString("1002005C5E16"), first.Writes.Last());
        first.Incoming.Enqueue([0xE5, ..ppiResponse.Select((b, i) => i == 21 ? (byte)5 : b)]);
        TestAssert.True(!(await ppi.WriteAsync("M100", new byte[] { 1, 2 })).IsSuccess);
        var mpi = new SiemensMpiSerialClient(transport, responseBoundary: new FixedLengthFrame(33));
        byte[] mpiAck = new byte[15]; mpiAck[14] = 0xE5;
        byte[] mpiData = new byte[33]; mpiData[25] = 0xFF; mpiData[26] = 4; mpiData[29] = 7; mpiData[30] = 8;
        first.Incoming.Enqueue([0xDC, 2, 2, 0xDC, 0, 2, ..mpiAck, ..mpiData]);
        TestAssert.Bytes([7, 8], (await mpi.ReadAsync("M100", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("6808086882805C1602B007002D16E5"), first.Writes.Last());
        transport.Close(); await transport.OpenAsync();
        second.Incoming.Enqueue([0xDC, 0, 2, ..mpiAck, ..mpiData]);
        TestAssert.Bytes([7, 8], (await mpi.ReadAsync("M100", 2)).Content);
        TestAssert.Bytes([0xDC, 2, 0], second.Writes.First());
    }
}
