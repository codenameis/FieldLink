using FieldLink.PlcDrivers.Modbus;
using System.Reflection;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Delta;
using FieldLink.PlcDrivers.Inovance;
using FieldLink.PlcDrivers.MegMeet;
using FieldLink.PlcDrivers.XINJE;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task DeltaDvpAsBoundariesStationAndInvalidAddressAsync()
    {
        Address(DeltaDvpAddressParser.ParseDeltaDvpAddress("D4095", 3), "8191");
        Address(DeltaDvpAddressParser.ParseDeltaDvpAddress("s=2;D4096", 3), "s=2;36864");
        Address(DeltaDvpAddressParser.ParseDeltaDvpAddress("M1535", 1), "3583");
        Address(DeltaDvpAddressParser.ParseDeltaDvpAddress("M1536", 1), "45056");
        Address(DeltaDvpAddressParser.ParseDeltaDvpAddress("X17", 1), "x=2;1039");
        Address(DeltaAsAddressParser.ParseDeltaASAddress("s=3;X1.15", 1), "s=3;x=2;24607");
        Address(DeltaAsAddressParser.ParseDeltaASAddress("SM0", 1), "16384");
        Failed(DeltaDvpAddressParser.ParseDeltaDvpAddress("X18", 1));
        Failed(DeltaAsAddressParser.ParseDeltaASAddress("INVALID", 3));
        return Task.CompletedTask;
    }

    internal static Task InovanceAmH3uH5uAddressConversionsAsync()
    {
        Address(InovanceAddressParser.PraseInovanceAddress(InovanceSeries.AM, "s=2;QX3.7", 1), "s=2;31");
        Address(InovanceAddressParser.PraseInovanceAMAddress("MD100", 3), "200");
        Address(InovanceAddressParser.PraseInovanceAMAddress("MX3.7", 1), "1.15");
        Address(InovanceAddressParser.PraseInovanceH3UAddress("C199", 3), "62663");
        Address(InovanceAddressParser.PraseInovanceH3UAddress("C200", 3), "63232");
        Address(InovanceAddressParser.PraseInovanceH5UAddress("s=4;X17", 1), "s=4;63503");
        Address(InovanceAddressParser.PraseInovanceH5UAddress("R100", 3), "12388");
        foreach (var series in new[] { InovanceSeries.AM, InovanceSeries.H3U, InovanceSeries.H5U })
            Failed(InovanceAddressParser.PraseInovanceAddress(series, "INVALID", 3));
        Failed(InovanceAddressParser.PraseInovanceH5UAddress("X18", 1));
        return Task.CompletedTask;
    }

    internal static Task MegMeetRangesAndInvalidAddressAsync()
    {
        // 공통 변환기는 내부 계약이므로 기존 golden 검증과 같은 방식으로 호출한다.
        var parse = (Func<string, byte, OperationResult<string>>)typeof(MegMeetAddressParser)
            .GetMethod("PraseMegMeetAddress", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate(typeof(Func<string, byte, OperationResult<string>>));
        Address(parse("M2047", 1), "4047");
        Address(parse("M2048", 1), "12000");
        Address(parse("SM255", 1), "4655");
        Address(parse("SM256", 1), "30000");
        Address(parse("s=5;X17", 1), "s=5;x=2;15");
        Address(parse("D100", 3), "100");
        Failed(parse("X18", 1));
        Failed(parse("INVALID", 3));
        return Task.CompletedTask;
    }

    internal static Task XinjeXcXdXlAddressConversionsAsync()
    {
        Address(XinjeAddressParser.PraseXinJEAddress(XinJESeries.XC, "X17", 1), "16399");
        Address(XinjeAddressParser.PraseXinJEAddress(XinJESeries.XC, "M8000", 1), "24576");
        foreach (var series in new[] { XinJESeries.XC, XinJESeries.XD, XinJESeries.XL })
        {
            Address(XinjeAddressParser.PraseXinJEAddress(series, "s=3;D100", 3), "s=3;100");
            Failed(XinjeAddressParser.PraseXinJEAddress(series, "X18", 1));
            Failed(XinjeAddressParser.PraseXinJEAddress(series, "INVALID", 3));
        }
        Address(XinjeAddressParser.PraseXinJEAddress(XinJESeries.XD, "X17", 1), "20495");
        Address(XinjeAddressParser.PraseXinJEAddress(XinJESeries.XL, "X10000", 1), "20736");
        return Task.CompletedTask;
    }

    internal static Task CommonChecksumsAndModbusEnvelopeAsync()
    {
        TestAssert.Bytes(H("01030000000AC5CD"), Crc16.Append(H("01030000000A")));
        TestAssert.True(Crc16.Verify(H("01030000000AC5CD")));
        TestAssert.True(!Crc16.Verify(H("01030000000AC5CC")));
        TestAssert.Bytes(H("01030000000AF2"), LrcChecksum.Append(H("01030000000A")));
        TestAssert.True(LrcChecksum.Verify(H("01030000000AF2")));
        TestAssert.True(!LrcChecksum.Verify(H("01030000000AF3")));
        TestAssert.Bytes(H("123400000006010300640002"), ModbusFrameRules.PackCommandToTcp(H("010300640002"), 0x1234));
        TestAssert.Bytes(H("010300640002"), ModbusFrameRules.ExplodeTcpCommandToCore(H("123400000006010300640002")));
        Payload(ModbusFrameRules.ExtractActualData(H("0103041234ABCD")), "1234ABCD");
        Failed(ModbusFrameRules.ExtractActualData(H("018302")), 2);
        Failed(ModbusFrameRules.ExtractActualData([]));
        TestAssert.True(ModbusFrameRules.CheckAsciiReceiveDataComplete(A(":0103021234B4\r\n")));
        TestAssert.True(!ModbusFrameRules.CheckAsciiReceiveDataComplete(A(":0103021234B4\r")));
        return Task.CompletedTask;
    }

    internal static Task CommonByteOrderAndSignedLimitsAsync()
    {
        TestAssert.Equal((ushort)0x1234, new ProtocolValueConverter().ReadUInt16(H("3412"), 0));
        TestAssert.Equal((ushort)0x1234, new ProtocolValueConverter(ByteOrder.BigEndian).ReadUInt16(H("1234"), 0));
        TestAssert.Equal((short)-32768, new ProtocolValueConverter(ByteOrder.BigEndian).ReadInt16(H("8000"), 0));
        TestAssert.Equal(0x12345678, new ProtocolValueConverter().ReadInt32(H("78563412"), 0));
        TestAssert.Equal(0x12345678, new ProtocolValueConverter(ByteOrder.BigEndian).ReadInt32(H("12345678"), 0));
        TestAssert.Equal(0x12345678, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap).ReadInt32(H("56781234"), 0));
        TestAssert.Bytes(H("0501"), ProtocolBytes.BoolArrayToByte([true, false, true, false, false, false, false, false, true]));
        TestAssert.True(ProtocolBytes.ByteToBoolArray(H("0501"), 9).SequenceEqual(new[] { true, false, true, false, false, false, false, false, true }));
        return Task.CompletedTask;
    }

    private static void Address(OperationResult<string> result, string expected)
    {
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal(expected, result.Content);
    }
}
