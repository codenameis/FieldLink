using System.Text;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.LSIS.Clients;
using FieldLink.PlcDrivers.Vigor.Clients;
using FieldLink.PlcDrivers.Freedom.Clients;
using FieldLink.PlcDrivers.Melsec.Clients;
using FieldLink.PlcDrivers.Melsec;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task LsisVigorAndCustomSerialFramesAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        void Queue(string frame) => port.Incoming.Enqueue(Encoding.ASCII.GetBytes(frame));
        var cnet = new LSCnetSerialClient(transport, 1);
        Queue("\u000601rSB01021234\u000300");
        TestAssert.Bytes([0x12, 0x34], (await cnet.ReadAsync("D100", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("05303172534230362544423230303032043736"), port.Writes.Last());
        Queue("\u001501rSB0001\u000300");
        TestAssert.True(!(await cnet.ReadAsync("D100", 2)).IsSuccess);
        var cpu = new LSCpuSerialClient(transport, 1);
        Queue("\u0006r123400\u0003");
        TestAssert.Bytes([0x12, 0x34], (await cpu.ReadAsync("D100", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("0272613030303030303033353603"), port.Writes.Last());
        var vigor = new VigorSerialClient(transport, 1);
        // 원본 VS 길이 필드: 상태 1바이트 + 데이터 4바이트. 10 10은 이스케이프된 데이터 10입니다.
        port.Incoming.Enqueue(TestBytes.FromHexString("100201050000101003341210034545"));
        TestAssert.Bytes([0x10, 3, 0x34, 0x12], (await vigor.ReadAsync("D100", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("100201070020A0000100020010034342"), port.Writes.Last());
        var custom = new FreedomSerialClient(transport, new FixedLengthFrame(3));
        port.Incoming.Enqueue([1, 2, 3]); TestAssert.Bytes([2, 3], (await custom.ReadAsync("stx=1;AABB")).Content);
        TestAssert.Bytes([0xAA, 0xBB], port.Writes.Last());
    }

    internal static async Task A3cSerialWordReadAndWriteErrorAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        var device = new MelsecA3CSerialClient(transport, new A3CFrameOptions { Station = 1, SumCheck = false });
        port.Incoming.Enqueue(Encoding.ASCII.GetBytes("\u0002F90100FF001234ABCD\u0003"));
        var words = await device.ReadAsync("D100", 2);
        TestAssert.True(words.IsSuccess, words.Message); TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], words.Content);
        TestAssert.Equal("\u0005F90100FF0004010000D*0001000002", Encoding.ASCII.GetString(port.Writes.Last()));
        port.Incoming.Enqueue(Encoding.ASCII.GetBytes("\u0015F90100FF00C051"));
        var error = await device.WriteAsync("D100", new byte[] { 1, 2 });
        TestAssert.True(!error.IsSuccess); TestAssert.Equal(0xC051, error.ErrorCode);
    }
}
