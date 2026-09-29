using FieldLink.PlcDrivers.Modbus;
using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task DeltaInvalidWriteDoesNotSendPartialDataAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var delta = new FieldLink.PlcDrivers.Delta.Clients.DeltaSerialClient(transport);
        // 잘못된 구현이 첫 구간을 보낼 경우 승인한 뒤 다음 구간의 인수 오류까지 도달하게 합니다.
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("01101FFE0002")));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => delta.WriteAsync("D4094", new byte[7]));
        TestAssert.Equal(0, port.Writes.Count);
    }

    internal static async Task ManufacturerModbusAddressesAndBoundariesAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port);
        await transport.OpenAsync();
        void Queue(string core) => port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString(core)));
        byte[] LastCore() => TestBytes.Slice(port.Writes.Last(), 0, trimEnd: 2);
        var delta = new FieldLink.PlcDrivers.Delta.Clients.DeltaSerialClient(transport);
        Queue("0203021122"); Queue("0203023344");
        var split = await delta.ReadAsync("s=2;D4095", 2);
        TestAssert.True(split.IsSuccess, split.Message); TestAssert.Bytes([0x11, 0x22, 0x33, 0x44], split.Content);
        TestAssert.Bytes(TestBytes.FromHexString("02031FFF0001"), TestBytes.Slice(port.Writes.ElementAt(0), 0, trimEnd: 2));
        TestAssert.Bytes(TestBytes.FromHexString("020390000001"), LastCore());
        Queue("018302"); int before = port.Writes.Count;
        TestAssert.True(!(await delta.ReadAsync("D4095", 2)).IsSuccess);
        TestAssert.Equal(before + 1, port.Writes.Count);
        var meg = new FieldLink.PlcDrivers.MegMeet.Clients.MegMeetSerialClient(transport);
        Queue("01010101"); Queue("01010100");
        var bits = await meg.ReadBoolAsync("M2047", 2);
        TestAssert.True(bits.IsSuccess, bits.Message); TestAssert.True(bits.Content[0] && !bits.Content[1]);
        TestAssert.Bytes(TestBytes.FromHexString("01012EE00001"), LastCore());
        before = port.Writes.Count; Queue("01010101"); Queue("01010100");
        var lower = await meg.ReadBoolAsync("m2047", 2);
        TestAssert.True(lower.IsSuccess, lower.Message); TestAssert.Equal(before + 2, port.Writes.Count);
        TestAssert.True(lower.Content[0] && !lower.Content[1]);
        var xinje = new FieldLink.PlcDrivers.XINJE.Clients.XinJESerialClient(transport);
        Queue("010302AABB"); TestAssert.True((await xinje.ReadAsync("D100", 1)).IsSuccess);
        TestAssert.Bytes(TestBytes.FromHexString("010300640001"), LastCore());
        var inovance = new FieldLink.PlcDrivers.Inovance.Clients.InovanceSerialClient(transport);
        Queue("0103021234"); TestAssert.Equal((byte)0x34, (await inovance.ReadByteAsync("MB2")).Content);
        TestAssert.Bytes(TestBytes.FromHexString("010300010001"), LastCore());
        Queue("0133025678"); var system = await inovance.ReadAsync("SDW10", 1);
        TestAssert.True(system.IsSuccess, system.Message); TestAssert.Bytes([0x56, 0x78], system.Content);
        TestAssert.Bytes(TestBytes.FromHexString("0133000A0001"), LastCore());
    }

    internal static async Task ModbusSerialRtuAndAsciiValidateFramesAsync()
    {
        foreach (var mode in new[] { ModbusSerialEncoding.Rtu, ModbusSerialEncoding.Ascii })
        {
            var port = new FakeSerialPort(); using var transport = NewSerial(port);
            var device = new ModbusSerialClient(transport, mode);
            await transport.OpenAsync();
            port.Incoming.Enqueue(mode == ModbusSerialEncoding.Rtu
                ? TestBytes.FromHexString("0103021234B533") : System.Text.Encoding.ASCII.GetBytes(":0103021234B4\r\n"));
            var result = await device.ReadAsync("0", 1);
            TestAssert.True(result.IsSuccess, result.Message);
            TestAssert.Bytes([0x12, 0x34], result.Content);
            TestAssert.Bytes(mode == ModbusSerialEncoding.Rtu ? TestBytes.FromHexString("010300000001840A")
                : System.Text.Encoding.ASCII.GetBytes(":010300000001FB\r\n"), port.Writes.Last());
            port.Incoming.Enqueue(mode == ModbusSerialEncoding.Rtu ? TestBytes.FromHexString("018302C0F1")
                : System.Text.Encoding.ASCII.GetBytes(":0183027A\r\n"));
            var error = await device.ReadAsync("0", 1);
            TestAssert.True(!error.IsSuccess); TestAssert.Equal(2, error.ErrorCode);
            port.Incoming.Enqueue(mode == ModbusSerialEncoding.Rtu ? TestBytes.FromHexString("01030212340000")
                : System.Text.Encoding.ASCII.GetBytes(":010302123400\r\n"));
            TestAssert.True(!(await device.ReadAsync("0", 1)).IsSuccess);
        }
    }
}
