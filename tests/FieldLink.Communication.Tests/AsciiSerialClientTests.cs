using System.Text;
using FieldLink.PlcDrivers.FATEK.Clients;
using FieldLink.PlcDrivers.Fuji.Clients;
using FieldLink.PlcDrivers.Panasonic.Clients;
using FieldLink.PlcDrivers.Yamatake.Clients;

namespace FieldLink.Communication.Tests;
internal static partial class SerialTests
{
    internal static async Task AsciiManufacturerReadAndErrorFramesAsync()
    {
        var port = new FakeSerialPort(); using var transport = NewSerial(port); await transport.OpenAsync();
        void Queue(string frame) => port.Incoming.Enqueue(Encoding.ASCII.GetBytes(frame));
        var fatek = new FatekProgramSerialClient(transport);
        Queue("\u0002014601234ABCDD1\u0003");
        TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], (await fatek.ReadAsync("D100", 2)).Content);
        TestAssert.Bytes(TestBytes.FromHexString("02303134363032443030313030363403"), port.Writes.Last());
        Queue("\u00020144200\u0003");
        var denied = await fatek.ReadBoolAsync("M0", 1);
        TestAssert.True(!denied.IsSuccess); TestAssert.Equal((int)'2', denied.ErrorCode);
        Queue("\u000201440" + new string('1', 255) + "00\u0003"); Queue("\u000201440000\u0003");
        var bits = await fatek.ReadBoolAsync("M0", 256);
        TestAssert.True(bits.IsSuccess, bits.Message); TestAssert.Equal(256, bits.Content.Length);
        TestAssert.True(bits.Content[254] && !bits.Content[255]);
        var fuji = new FujiSpbSerialClient(transport);
        // ':' + 국번 + 길이 + 라우팅 4문자 + 상태 + 읽기 메타데이터 + 워드 값.
        Queue(":01" + "09" + "0000" + "00" + "0000" + "3412CDAB\r\n");
        var words = await fuji.ReadAsync("D100", 2);
        TestAssert.True(words.IsSuccess, words.Message); TestAssert.Bytes([0x34, 0x12, 0xCD, 0xAB], words.Content);
        TestAssert.Bytes(TestBytes.FromHexString("3A303130394646464630303030304330303031303230300D0A"), port.Writes.Last());
        var panasonic = new PanasonicMewtocolSerialClient(transport, station: 1);
        Queue("%01$RC100\r"); TestAssert.True((await panasonic.ReadBoolAsync("R100")).Content);
        TestAssert.Bytes(TestBytes.FromHexString("25303123524353523031303031360D"), port.Writes.Last());
        Queue("%01!40000\r"); TestAssert.True(!(await panasonic.WriteAsync("R100", true)).IsSuccess);
        var digitron = new DigitronCplSerialClient(transport);
        Queue("\u00020100X00,4660,-2\u000300\r\n");
        var data = await digitron.ReadAsync("100", 2);
        TestAssert.True(data.IsSuccess, data.Message); TestAssert.Bytes([0x34, 0x12, 0xFE, 0xFF], data.Content);
        TestAssert.Bytes(TestBytes.FromHexString("02303130305852532C313030572C320343420D0A"), port.Writes.Last());
    }
}
