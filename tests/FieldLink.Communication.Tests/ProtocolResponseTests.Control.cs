using FieldLink.PlcDrivers.FATEK;
using FieldLink.PlcDrivers.Keyence;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Panasonic;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task FatekControlStatusBitsAndErrorAsync()
    {
        var result = FatekProgramControlResponseParser.ParseReadStatus(A("\u000201400A500\u0003"), 1);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.True(result.Content.SequenceEqual(new[] { true, false, true, false, false, true, false, true }));
        Failed(FatekProgramControlResponseParser.ParseReadStatus(A("\u0002014020000\u0003"), 1), '2');
        return Task.CompletedTask;
    }

    internal static Task KeyenceControlTypeModeAnnotationAndErrorAsync()
    {
        var type = KeyenceNanoControlResponseParser.ParseReadPlcType(A("53\r\n"));
        TestAssert.True(type.IsSuccess, type.Message);
        TestAssert.Equal(KeyencePLCS.KV5500, type.Content);
        Failed(KeyenceNanoControlResponseParser.ParseReadPlcType(A("99\r\n")));
        TestAssert.Equal(0, KeyenceNanoControlResponseParser.ParseReadPlcMode(A("0\r\n")).Content);
        TestAssert.Equal(1, KeyenceNanoControlResponseParser.ParseReadPlcMode(A("1\r\n")).Content);
        TestAssert.Equal("PUMP", KeyenceNanoControlResponseParser.ParseReadAddressAnnotation(A("  PUMP  \r\n"), "DM100").Content);
        Payload(KeyenceNanoControlResponseParser.ParseReadExpansionMemory(A("4660 43981\r\n"), 1, 100, 2), "3412CDAB");
        Failed(KeyenceNanoControlResponseParser.ParseReadPlcMode(A("E1\r\n")));
        return Task.CompletedTask;
    }

    internal static Task HostLinkCModeControlStatusAndErrorsAsync()
    {
        var mode = OmronHostLinkCModeControlResponseParser.ParseReadPlcMode(A("@01MS00030000*\r"), 1);
        TestAssert.True(mode.IsSuccess, mode.Message);
        TestAssert.Equal(3, mode.Content);
        TestAssert.True(OmronHostLinkCModeControlResponseParser.ParseChangePlcMode(A("@01SC0000*\r"), 1, 2).IsSuccess);
        Failed(OmronHostLinkCModeControlResponseParser.ParseChangePlcMode(A("@01SC0100*\r"), 1, 2), 1);
        Failed(OmronHostLinkCModeControlResponseParser.ParseReadPlcType(A("@01MM010000*\r"), 1), 1);
        return Task.CompletedTask;
    }

    internal static Task PanasonicMcPayloadAndErrorAsync()
    {
        Payload(PanasonicMcNetResponseParser.UnpackResponseContent([], H("D00000FFFF0300060000003412CDAB")), "3412CDAB");
        Failed(PanasonicMcNetResponseParser.UnpackResponseContent([], H("D00000FFFF0300020056C0")), 0xC056);
        return Task.CompletedTask;
    }

    internal static Task MelsecSerialServerRequestEnvelopeAsync()
    {
        // A3C 형식 1: ENQ F9 국번 네트워크 PC 모듈, 이후 MC 본문.
        var a3c = new A3CResponseOptions { Station = 1, Format = 1, SumCheck = false };
        byte[] request = A("\u0005" + "F9" + "01" + "00" + "FF" + "00" + "04010000D*0001000002");
        TestAssert.Bytes(A("04010000D*0001000002"), MelsecA3CServerResponseParser.ExtraMcCore(a3c, request).Content);
        a3c.Station = 2;
        Failed(MelsecA3CServerResponseParser.ExtraMcCore(a3c, request));
        var fx = new FxLinksResponseOptions { Station = 1, SumCheck = false };
        TestAssert.Bytes(A("WR0D010002"), MelsecFxLinksServerResponseParser.ExtraMcCore(fx, A("\u000501FFWR0D010002"), 1).Content);
        TestAssert.Bytes(A("WR0D010002"), MelsecFxLinksServerResponseParser.ExtraMcCore(fx, A("\u000501FFWR0D010002\r\n"), 4).Content);
        Failed(MelsecFxLinksServerResponseParser.ExtraMcCore(fx, A("\u000501FFWR0D010002\rX"), 4));
        fx.Station = 2;
        Failed(MelsecFxLinksServerResponseParser.ExtraMcCore(fx, A("\u000501FFWR0D010002"), 1));
        return Task.CompletedTask;
    }

    internal static Task A3CServerReadsHexadecimalStationInAllFormatsAsync()
    {
        foreach (var station in new (byte Value, string Text)[] { (0x0A, "0A"), (0x10, "10"), (0x7F, "7F"), (0xFF, "FF") })
        {
            foreach (int format in new[] { 1, 2, 3, 4 })
            {
                string prefix = format == 3 ? "\u0002" : "\u0005";
                if (format == 2)
                    prefix += "00";
                string suffix = format == 3 ? "\u0003" : format == 4 ? "\r\n" : "";
                byte[] request = A(prefix + "F9" + station.Text + "00FF00" + "04010000D*0001000002" + suffix);
                var options = new A3CResponseOptions { Station = station.Value, Format = format, SumCheck = false };
                var result = MelsecA3CServerResponseParser.ExtraMcCore(options, request);
                TestAssert.True(result.IsSuccess, result.Message);
                TestAssert.Bytes(A("04010000D*0001000002"), result.Content);
            }
        }
        return Task.CompletedTask;
    }
}
