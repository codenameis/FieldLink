using FieldLink.PlcDrivers.Fuji;
using FieldLink.PlcDrivers.LSIS;
using FieldLink.PlcDrivers.YASKAWA;
using FieldLink.PlcDrivers.Yokogawa;
using FieldLink.PlcDrivers.Yamatake;
using FieldLink.PlcDrivers.Toyota;
using FieldLink.PlcDrivers.Panasonic;
using FieldLink.PlcDrivers.Vigor;
using FieldLink.PlcDrivers.Keyence;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task FujiCommandSettingReadWriteAndErrorsAsync()
    {
        Payload(FujiCommandSettingTypeResponseParser.UnpackResponseContentHelper([], H("000000000000000000003412CDAB")), "3412CDAB");
        Payload(FujiCommandSettingTypeResponseParser.UnpackResponseContentHelper([], [1, 0]), "");
        Failed(FujiCommandSettingTypeResponseParser.UnpackResponseContentHelper([], [0, 2]));
        Failed(FujiCommandSettingTypeResponseParser.UnpackResponseContentHelper([], [0, 0]));
        return Task.CompletedTask;
    }

    internal static Task FujiSpbReadAndErrorsAsync()
    {
        // SPB 검증기는 11바이트 ASCII 헤더와 CR/LF를 제거하고 ASCII 본문을 돌려준다.
        TestAssert.Bytes(A("3412CDAB"), FujiSPBResponseParser.CheckResponseData(A(":01090000003412CDAB\r\n")).Content);
        Failed(FujiSPBResponseParser.CheckResponseData(A(":010500000100\r\n")), 1);
        Failed(FujiSPBResponseParser.CheckResponseData([]));
        return Task.CompletedTask;
    }

    internal static Task FujiSphReadWriteAndErrorsAsync()
    {
        byte[] frame = new byte[30];
        H("3412CDAB").CopyTo(frame, 26);
        Payload(FujiSPHNetResponseParser.ExtractActualData(frame), "3412CDAB");
        Payload(FujiSPHNetResponseParser.ExtractActualData(TestBytes.Slice(frame, 0, 26)), "");
        frame[4] = 0x11;
        Failed(FujiSPHNetResponseParser.ExtractActualData(frame), 0x11);
        Failed(FujiSPHNetResponseParser.ExtractActualData([]));
        return Task.CompletedTask;
    }

    internal static Task LsFastEnetReadCpuStatusAndErrorsAsync()
    {
        // XGT header and application lengths are part of the response, including on NAKs.
        byte[] frame = LsStandardsFrame("5500140000000000010004003412CDAB");
        var context = new FastEnetFrameOptions();
        Payload(LSFastEnetResponseParser.ExtractActualData(context, frame), "3412CDAB");
        TestAssert.Equal("XGK/R-CPUH", context.CpuType);
        TestAssert.Equal(LSCpuStatus.RUN, context.LSCpuStatus);
        TestAssert.True(!context.CpuError);
        frame = LsStandardsFrame("59001400000000000100");
        frame[11] = 2;
        Payload(LSFastEnetResponseParser.ExtractActualData(context, frame), "");
        TestAssert.Equal(LSCpuStatus.STOP, context.LSCpuStatus);
        frame = LsStandardsFrame("590014000000FFFF21");
        Failed(LSFastEnetResponseParser.ExtractActualData(context, frame), 0x21);
        Failed(LSFastEnetResponseParser.ExtractActualData(context, new byte[19]));
        frame = LsStandardsFrame("550014000000000001000A003412CDAB");
        Failed(LSFastEnetResponseParser.ExtractActualData(context, frame));
        return Task.CompletedTask;
    }

    internal static Task LsCnetContinuousIndividualAndErrorsAsync()
    {
        Payload(LSCnetResponseParser.UnpackResponseContent([], A("\u000601rSB01043412CDAB\u0003")), "3412CDAB");
        Payload(LSCnetResponseParser.UnpackResponseContent([], A("\u000601rSS0202341202CDAB\u0003")), "3412CDAB");
        Failed(LSCnetResponseParser.UnpackResponseContent([], A("\u001501rSB0003")), 3);
        Failed(LSCnetResponseParser.UnpackResponseContent([], []));
        Failed(LSCnetResponseParser.UnpackResponseContent([], A("\u000601rSB010434")));
        return Task.CompletedTask;
    }

    internal static Task LsComputerLinkReadWriteAndErrorsAsync()
    {
        Payload(LSCpuResponseParser.UnpackResponseContent([], A("\u0006s3412CDAB00\u0004")), "3412CDAB");
        byte[] write = A("\u0006w00\u0004");
        TestAssert.Bytes(write, LSCpuResponseParser.UnpackResponseContent([], write).Content);
        Failed(LSCpuResponseParser.UnpackResponseContent([], A("\u001501rSB0003")), 3);
        Failed(LSCpuResponseParser.UnpackResponseContent([], []));
        return Task.CompletedTask;
    }

    internal static Task YokogawaLinkReadWriteAndErrorsAsync()
    {
        Payload(YokogawaLinkTcpResponseParser.CheckContent(H("910000041234ABCD")), "1234ABCD");
        Payload(YokogawaLinkTcpResponseParser.CheckContent(H("92000000")), "");
        Failed(YokogawaLinkTcpResponseParser.CheckContent(H("91010000")));
        Failed(YokogawaLinkTcpResponseParser.CheckContent([0x91]));
        return Task.CompletedTask;
    }

    internal static Task ToyotaToyoPucReadWriteAndErrorsAsync()
    {
        Payload(ToyoPucResponseParser.UnpackResponseContent([], H("80000500943412CDAB")), "3412CDAB");
        Payload(ToyoPucResponseParser.UnpackResponseContent([], H("8000010095")), "");
        Failed(ToyoPucResponseParser.UnpackResponseContent([], H("8001020001")));
        Failed(ToyoPucResponseParser.UnpackResponseContent([], H("8100010094")));
        Failed(ToyoPucResponseParser.UnpackResponseContent([], [0x80]));
        return Task.CompletedTask;
    }

    internal static Task MemobusResponseServiceAndErrorAsync()
    {
        // SIEP C88070004Q Appendix B: 0x19 response and two explicit length fields.
        byte[] request = H("11210000000016000000000008002009000000000200");
        byte[] response = H("1921000000001800000000000A002009000002003412CDAB");
        // MEMOBUS 파서는 12바이트 전송 헤더를 제거한 명령 본문을 돌려준다.
        Payload(MemobusResponseParser.UnpackResponseContent(request, response), "0A002009000002003412CDAB");
        response = H("192100000000120000000000040020890002");
        Failed(MemobusResponseParser.UnpackResponseContent(request, response), 2);
        response[15] = 4;
        Failed(MemobusResponseParser.UnpackResponseContent(request, response), 4);
        return Task.CompletedTask;
    }

    internal static Task YamatakeCplSignedWordsAndErrorsAsync()
    {
        Payload(DigitronCPLResponseParser.ExtraActualResponse(A("\u00020100\u000200,4660,-1,-32768\u0003")), "3412FFFF0080");
        Payload(DigitronCPLResponseParser.ExtraActualResponse(A("\u00020100\u000200\u0003")), "");
        Failed(DigitronCPLResponseParser.ExtraActualResponse(A("\u00020100\u000201\u0003")), 1);
        Failed(DigitronCPLResponseParser.ExtraActualResponse(A("\u00020100\u000200,32768\u0003")));
        Failed(DigitronCPLResponseParser.ExtraActualResponse([]));
        return Task.CompletedTask;
    }

    internal static Task PanasonicMewtocolWordsBitsAndErrorsAsync()
    {
        Payload(PanasonicResponseParser.ExtraActualData(A("%01$RD3412CDAB00\r")), "3412CDAB");
        var bits = PanasonicResponseParser.ExtraActualBool(A("%01$RC10100\r"));
        TestAssert.True(bits.IsSuccess, bits.Message);
        TestAssert.True(bits.Content.SequenceEqual(new[] { true, false, true }));
        Failed(PanasonicResponseParser.ExtraActualData(A("%01!2100\r")), 21);
        Failed(PanasonicResponseParser.ExtraActualBool(A("%01!2100\r")), 21);
        Failed(PanasonicResponseParser.ExtraActualData([]));
        return Task.CompletedTask;
    }

    internal static Task VigorVsEscapingLengthAndErrorsAsync()
    {
        byte[] frame = H("10020105000034121010AB10030000");
        Payload(VigorVsResponseParser.CheckResponseContent(frame), "341210AB");
        TestAssert.True(VigorVsResponseParser.CheckReceiveDataComplete(frame, frame.Length));
        TestAssert.True(!VigorVsResponseParser.CheckReceiveDataComplete(frame, frame.Length - 1));
        frame[5] = 4;
        Failed(VigorVsResponseParser.CheckResponseContent(frame), 4);
        frame[5] = 0; frame[3] = 6;
        Failed(VigorVsResponseParser.CheckResponseContent(frame));
        Failed(VigorVsResponseParser.CheckResponseContent([]));
        return Task.CompletedTask;
    }

    internal static Task KeyenceNanoWordsBitsAndErrorsAsync()
    {
        TestAssert.True(KeyenceNanoResponseParser.CheckPlcReadResponse(A("4660 43981\r\n")).IsSuccess);
        Payload(KeyenceNanoResponseParser.ExtractActualData("DM", A("4660 43981\r\n")), "3412CDAB");
        Payload(KeyenceNanoResponseParser.ExtractActualData("AT", A("305419896\r\n")), "78563412");
        var bits = KeyenceNanoResponseParser.ExtractActualBoolData("MR", A("1 0 1\r\n"));
        TestAssert.True(bits.IsSuccess && bits.Content.SequenceEqual(new[] { true, false, true }));
        TestAssert.True(KeyenceNanoResponseParser.CheckPlcWriteResponse(A("OK\r\n")).IsSuccess);
        Failed(KeyenceNanoResponseParser.CheckPlcReadResponse(A("E1\r\n")));
        Failed(KeyenceNanoResponseParser.CheckPlcWriteResponse(A("E1\r\n")));
        Failed(KeyenceNanoResponseParser.ExtractActualData("DM", A("65536\r\n")));
        Failed(KeyenceNanoResponseParser.CheckPlcReadResponse([]));
        return Task.CompletedTask;
    }
}
