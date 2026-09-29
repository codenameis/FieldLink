using System.Text;
using FieldLink.PlcDrivers.Knx;
using FieldLink.PlcDrivers.Turck;
using FieldLink.PlcDrivers.IDCard;
using FieldLink.PlcDrivers.Geniitek;
using FieldLink.PlcDrivers.Keyence;
using FieldLink.PlcDrivers.Sick;
using FieldLink.PlcDrivers.Toledo;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task KnxMultiBytePayloadLengthDoesNotDependOnGroupAddressAsync()
    {
        // cEMI NPDU 길이 3은 2바이트 데이터다. 그룹 주소 하위 바이트는 길이가 아니다.
        foreach (byte addressLow in new byte[] { 0, 1, 52, 255 })
        {
            byte[] frame = H("061004200017040507002900BCE0000012000300801234");
            frame[17] = addressLow;
            var result = KnxResponseParser.Parse(5, true, frame);
            TestAssert.Equal((short)(0x1200 + addressLow), result.Address);
            TestAssert.Equal((byte)3, result.DataLength);
            TestAssert.Bytes(H("1234"), result.Data);
            TestAssert.Bytes(H("06100421000A04050700"), result.Reply);
        }
        return Task.CompletedTask;
    }

    internal static Task KnxConnectionAndStateReplyAsync()
    {
        var connected = KnxResponseParser.Parse(0, false, H("0610020600080500"));
        TestAssert.True(connected.IsConnected);
        TestAssert.Equal((byte)5, connected.Channel);
        var rejected = KnxResponseParser.Parse(0, false, H("0610020600080025"));
        TestAssert.True(!rejected.IsConnected);
        var state = KnxResponseParser.Parse(5, true, H("061002070010050008017F0000010E57"));
        TestAssert.Bytes(H("0610020800080500"), state.Reply);
        return Task.CompletedTask;
    }

    internal static Task GeniitekChecksumStationAndSignedAxesAsync()
    {
        byte[] frame = H("AA557F123401010001010000267FAAED");
        TestAssert.True(VibrationSensorClientResponseParser.CheckXor(frame));
        TestAssert.Equal((ushort)0x1234, VibrationSensorResponseParser.ParseStation(frame));
        frame[8] ^= 1;
        TestAssert.True(!VibrationSensorClientResponseParser.CheckXor(frame));
        var actual = VibrationSensorResponseParser.ParseActual(H("AA0064FF9C01367FAAED"));
        TestAssert.Equal(1f, actual.AcceleratedSpeedX);
        TestAssert.Equal(-1f, actual.AcceleratedSpeedY);
        TestAssert.Equal(3.10f, actual.AcceleratedSpeedZ);
        var peak = VibrationSensorResponseParser.ParsePeak(H("64009CFFC8002C01D4FE9001640038FF2C0160364A012C010000"));
        TestAssert.Equal(-1f, peak.AcceleratedSpeedY);
        TestAssert.Equal(-3f, peak.SpeedY);
        TestAssert.Equal(-200f, peak.OffsetY);
        TestAssert.Equal(3.3f, peak.Voltage);
        TestAssert.Equal(300, peak.SendingInterval);
        return Task.CompletedTask;
    }

    internal static Task SamHeaderLengthChecksumAndDeviceErrorAsync()
    {
        byte[] frame = H("AAAAAA96690003200122");
        TestAssert.True(SAMSerialResponseParser.CheckADSCommandAndSum(frame).IsSuccess);
        TestAssert.True(SAMSerialResponseParser.CheckADSCommandCompletion(frame.ToList()));
        TestAssert.True(!SAMSerialResponseParser.CheckADSCommandCompletion(TestBytes.Slice(frame, 0, trimEnd: 1).ToList()));
        frame[frame.Length - 1] ^= 1;
        Failed(SAMSerialResponseParser.CheckADSCommandAndSum(frame));
        Failed(SAMSerialResponseParser.CheckADSCommandAndSum(H("AAAAAA96690004200122")));
        Failed(SAMSerialResponseParser.CheckADSCommandAndSum(H("00AAAA96690003200122")));
        Failed(SAMSerialResponseParser.CheckADSCommandAndSum([]));
        byte[] error = new byte[10]; error[9] = 0x80;
        Failed(SAMSerialResponseParser.ExtractIdentityCard(error));
        Failed(SAMSerialResponseParser.ExtractSafeModuleNumber(error));
        return Task.CompletedTask;
    }

    internal static Task SamIdentityCardFieldsAsync()
    {
        // 실물 개인정보가 없는 고정된 가상 카드. 문자 영역은 UTF-16LE 256바이트다.
        string text = "테스트".PadRight(15) + "1" + "01" + "20000102" + "가상 주소".PadRight(35)
            + "000000000000000000" + "시험 기관".PadRight(15) + "20200101" + "20300101";
        byte[] frame = new byte[1294]; frame[9] = 0x90;
        Encoding.Unicode.GetBytes(text.PadRight(128)).CopyTo(frame, 14);
        for (int index = 270; index < 270 + 1024; index++)
            frame[index] = 0x5A;
        var read = SAMSerialResponseParser.ExtractIdentityCard(frame);
        TestAssert.True(read.IsSuccess, read.Message);
        TestAssert.Equal("테스트", read.Content.Name.Trim());
        TestAssert.Equal("남성", read.Content.Sex);
        TestAssert.Equal(new DateTime(2000, 1, 2), read.Content.Birthday);
        TestAssert.Equal(new DateTime(2030, 1, 1), read.Content.ValidityEndDate);
        TestAssert.True(read.Content.Portrait.Length == 1024 && read.Content.Portrait.All(value => value == 0x5A));
        Failed(SAMSerialResponseParser.ExtractIdentityCard(TestBytes.Slice(frame, 0, 100)));
        return Task.CompletedTask;
    }

    internal static Task TurckCrcUidAndTagRemovalAsync()
    {
        byte[] checkedFrame = H("AA09096800010258B2");
        TestAssert.True(ReaderNetResponseParser.CheckCRC(checkedFrame, 7));
        checkedFrame[5] ^= 1;
        TestAssert.True(!ReaderNetResponseParser.CheckCRC(checkedFrame, 7));
        var tag = new TurckTagInfo();
        byte[] uid = H("AA131368000102030405060708000010030000");
        var result = ReaderNetResponseParser.ExtraUID(tag, uid);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal("0102030405060708", result.Content);
        TestAssert.True(tag.successfullyInitialized);
        TestAssert.Equal((byte)16, tag.NumberOfBlock);
        TestAssert.Equal((byte)4, tag.BytesOfBlock);
        Failed(ReaderNetResponseParser.CheckResponseContent(tag, H("AA0A0A68000002000000")));
        TestAssert.True(!tag.successfullyInitialized);
        Payload(ReaderNetResponseParser.CheckResponseContent(tag, H("AA070768000000")), "");
        return Task.CompletedTask;
    }

    internal static Task KeyenceDlen1AndScannerErrorsAsync()
    {
        TestAssert.True(KeyenceDLEN1ResponseParser.CheckResponse(A("RD,01,123\r\n")).IsSuccess);
        foreach (int code in new[] { 9, 12, 14, 16, 20, 22, 31, 254, 255 })
            Failed(KeyenceDLEN1ResponseParser.CheckResponse(A($"ER,01,{code:D3}\r\n")), code);
        var barcode = Sr2000ResponseParser.Parse("LON", A("ABC123\r"));
        TestAssert.True(barcode.IsSuccess, barcode.Message);
        TestAssert.Equal("ABC123", barcode.Content);
        Failed(Sr2000ResponseParser.Parse("LON", A("ER,LON,01\r")));
        TestAssert.Equal("ABC123가나다", SickIcrTcpServerValueConverter.TranslateCode("\u0002ABC-123 가나다!\r\n"));
        TestAssert.Equal("", SickIcrTcpServerValueConverter.TranslateCode("\u0002-_\r\n"));
        return Task.CompletedTask;
    }

    internal static Task ToledoStatusUnitsAndExpandedWeightAsync()
    {
        byte[] frame = [2, 4, 0x1F, 0x18, ..A("001234000100\r")];
        var standard = new ToledoStandardData(frame);
        TestAssert.Equal(12.34f, standard.Weight);
        TestAssert.Equal("kg", standard.Unit);
        TestAssert.True(standard.Symbol && standard.Suttle && standard.BeyondScope && standard.DynamicState);
        TestAssert.True(standard.IsPrint && standard.IsTenExtend);
        byte[] expanded = [1, 0, 2, 3, 1, 0, ..A("   -12.34    1.00\r")];
        var value = new ToledoStandardData(expanded);
        TestAssert.True(value.IsExpandOutput && value.DataValid);
        TestAssert.Equal(-12.34f, value.Weight);
        TestAssert.Equal(1f, value.Tare);
        TestAssert.Equal("kg", value.Unit);
        return Task.CompletedTask;
    }
}
