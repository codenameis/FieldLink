using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Delta.Clients;
using FieldLink.PlcDrivers.Vigor.Clients;

namespace FieldLink.Communication.Tests;

internal static partial class SerialTests
{
    internal static async Task ReviewDeltaPreservesStationWithoutSplitAsync()
    {
        var port = new FakeSerialPort();
        using var transport = NewSerial(port);
        await transport.OpenAsync();
        var delta = new DeltaSerialClient(transport, station: 1);
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("0203021234")));
        var read = await delta.ReadAsync("s=2;D100", 1);
        TestAssert.Bytes(TestBytes.FromHexString("020310640001"), TestBytes.Slice(port.Writes.Last(), 0, trimEnd: 2));
        TestAssert.True(read.IsSuccess, read.Message);
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("021010640001")));
        TestAssert.True((await delta.WriteAsync("s=2;D100", new byte[] { 0, 1 })).IsSuccess);
        TestAssert.Bytes(TestBytes.FromHexString("021010640001020001"), TestBytes.Slice(port.Writes.Last(), 0, trimEnd: 2));
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("02010101")));
        TestAssert.True((await delta.ReadBoolAsync("s=2;M100", 1)).IsSuccess);
        TestAssert.Equal((byte)2, port.Writes.Last()[0]);
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("020F08640001")));
        TestAssert.True((await delta.WriteAsync("s=2;M100", new[] { true })).IsSuccess);
        TestAssert.Equal((byte)2, port.Writes.Last()[0]);
    }

    internal static async Task ReviewModbusReadWriteRejectsMixedStationsBeforeIoAsync()
    {
        var port = new FakeSerialPort();
        using var transport = NewSerial(port);
        await transport.OpenAsync();
        var device = new ModbusSerialClient(transport);
        port.Incoming.Enqueue(Crc16.Append(TestBytes.FromHexString("0117021234")));
        var rejected = await device.ReadWriteAsync("s=1;0", 1, "s=2;10", [0, 1]);
        TestAssert.True(!rejected.IsSuccess, "서로 다른 국번은 송신 전에 거부해야 합니다.");
        TestAssert.Equal(0, port.Writes.Count);
        var accepted = await device.ReadWriteAsync("s=1;0", 1, "s=1;10", [0, 1]);
        TestAssert.True(accepted.IsSuccess, accepted.Message);
        TestAssert.Bytes(TestBytes.FromHexString("011700000001000A0001020001"), TestBytes.Slice(port.Writes.Last(), 0, trimEnd: 2));
    }

    internal static async Task ReviewVigorSplitReturnsExactBitCountAsync()
    {
        foreach (ushort count in new ushort[] { 1024, 1025, 1031, 1032 })
        {
            var port = new FakeSerialPort();
            using var transport = NewSerial(port);
            await transport.OpenAsync();
            for (int remaining = count; remaining > 0; remaining -= 1024)
            {
                int bytes = (Math.Min(1024, remaining) + 7) / 8;
                // 독립 응답: 국번 1, little-endian 본문 길이, 상태 0, 데이터 FF.
                var core = new byte[4 + bytes];
                core[0] = 1;
                core[1] = (byte)(bytes + 1);
                for (int index = 4; index < 4 + bytes; index++)
                    core[index] = 0xFF;
                byte sum = unchecked((byte)core.Sum(b => b));
                port.Incoming.Enqueue([0x10, 2, ..core, 0x10, 3, ..System.Text.Encoding.ASCII.GetBytes(sum.ToString("X2"))]);
            }
            var result = await new VigorSerialClient(transport, 1).ReadBoolAsync("M0", count);
            TestAssert.True(result.IsSuccess, result.Message);
            TestAssert.Equal((int)count, result.Content.Length);
            TestAssert.True(result.Content.All(value => value));
        }
    }
}
