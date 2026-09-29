using FieldLink.PlcDrivers.Melsec;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task McAsciiWordsBitsAndEndCodeAsync()
    {
        byte[] frame = A("D00000FF03FF00000C00001234ABCD");
        TestAssert.True(McAsciiResponseParser.CheckResponseContent(frame).IsSuccess);
        TestAssert.Bytes(H("3412CDAB"), McAsciiResponseParser.ExtractActualDataHelper(TestBytes.Slice(frame, 22), false));
        TestAssert.Bytes([1, 0, 1], McAsciiResponseParser.ExtractActualDataHelper(A("101"), true));
        Failed(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF000004C056")), 0xC056);
        Failed(McAsciiResponseParser.CheckResponseContent([]));
        return Task.CompletedTask;
    }

    internal static Task A1EBinaryWordsBitsAndErrorCodesAsync()
    {
        byte[] frame = H("81003412CDAB");
        TestAssert.True(MelsecA1ENetResponseParser.CheckResponseLegal(frame).IsSuccess);
        Payload(MelsecA1ENetResponseParser.ExtractActualData(frame, false), "3412CDAB");
        Payload(MelsecA1ENetResponseParser.ExtractActualData(H("80001011"), true), "01000101");
        Failed(MelsecA1ENetResponseParser.CheckResponseLegal(H("815B10")), 0x10);
        Failed(MelsecA1ENetResponseParser.CheckResponseLegal(H("8151")), 0x51);
        Failed(MelsecA1ENetResponseParser.CheckResponseLegal([0x81]));
        return Task.CompletedTask;
    }

    internal static Task A1EAsciiWordsBitsAndErrorCodesAsync()
    {
        byte[] frame = A("81001234ABCD");
        TestAssert.True(MelsecA1EAsciiNetResponseParser.CheckResponseLegal(frame).IsSuccess);
        Payload(MelsecA1EAsciiNetResponseParser.ExtractActualData(frame, false), "3412CDAB");
        Payload(MelsecA1EAsciiNetResponseParser.ExtractActualData(A("8000101"), true), "010001");
        Failed(MelsecA1EAsciiNetResponseParser.CheckResponseLegal(A("815B10")), 0x10);
        Failed(MelsecA1EAsciiNetResponseParser.CheckResponseLegal(A("8151")), 0x51);
        Failed(MelsecA1EAsciiNetResponseParser.CheckResponseLegal(A("81")));
        return Task.CompletedTask;
    }

    internal static Task A3CAllFourFormatsReadWriteAndErrorsAsync()
    {
        string[] headers = ["F90000FF00", "00F90000FF00", "F90000FF00QACK", "F90000FF00"];
        for (int format = 1; format <= 4; format++)
        {
            var options = new A3CFrameOptions { Format = format, SumCheck = false };
            byte[] read = A("\u0002" + headers[format - 1] + "1234ABCD\u0003");
            TestAssert.Bytes(A("1234ABCD"), MelsecA3CNetResponseParser.ExtraReadActualResponse(options, read).Content);
            byte[] write = A((format == 3 ? "\u0002" : "\u0006") + headers[format - 1] + (format == 3 ? "\u0003" : ""));
            TestAssert.True(MelsecA3CNetResponseParser.CheckWriteResponse(options, write).IsSuccess);
            byte[] error = A((format == 3 ? "\u0002" : "\u0015") + headers[format - 1].Replace("QACK", "QNAK") + "C056\u0003");
            Failed(MelsecA3CNetResponseParser.ExtraReadActualResponse(options, error), 0xC056);
            Failed(MelsecA3CNetResponseParser.CheckWriteResponse(options, error), 0xC056);
            Failed(MelsecA3CNetResponseParser.ExtraReadActualResponse(options, []));
        }
        return Task.CompletedTask;
    }

    internal static Task FxSerialChecksumAndEscapedDataAsync()
    {
        // 합계는 STX를 제외한 ASCII 본문과 ETX의 하위 8비트: D7.
        byte[] frame = A("\u00023412CDAB\u0003D7");
        TestAssert.True(MelsecFxSerialResponseParser.CheckPlcReadResponse(frame).IsSuccess);
        TestAssert.True(MelsecFxSerialResponseParser.CheckReceiveDataComplete(frame));
        Payload(MelsecFxSerialResponseParser.ExtractActualData(frame), "3412CDAB");
        TestAssert.True(!MelsecFxSerialResponseParser.CheckReceiveDataComplete(TestBytes.Slice(frame, 0, trimEnd: 1)));
        frame[frame.Length - 1] = (byte)'8';
        Failed(MelsecFxSerialResponseParser.CheckPlcReadResponse(frame));
        TestAssert.True(!MelsecFxSerialResponseParser.CheckReceiveDataComplete(frame));
        TestAssert.True(MelsecFxSerialResponseParser.CheckPlcWriteResponse([6]).IsSuccess);
        Failed(MelsecFxSerialResponseParser.CheckPlcWriteResponse([0x15]));
        Failed(MelsecFxSerialResponseParser.CheckPlcReadResponse([]));
        return Task.CompletedTask;
    }

    internal static Task FxLinksWordsWriteAndErrorAsync()
    {
        var read = MelsecFxLinksResponseParser.CheckPlcResponse(A("\u000200FF1234ABCD\u0003"));
        TestAssert.True(read.IsSuccess, read.Message);
        Payload(MelsecFxLinksResponseParser.ExtraResponse(read.Content), "3412CDAB");
        Payload(MelsecFxLinksResponseParser.CheckPlcResponse([6]), "");
        Failed(MelsecFxLinksResponseParser.CheckPlcResponse(A("\u001500FF10")), 0x10);
        Failed(MelsecFxLinksResponseParser.CheckPlcResponse([]));
        return Task.CompletedTask;
    }

    internal static Task FxGotEnvelopeAndTransparentModeAsync()
    {
        var options = new FxGotFrameOptions();
        Payload(MelsecFxSerialOverTcpResponseParser.UnpackResponseContent(options, [], H("3412")), "3412");
        options.useGot = true;
        byte[] envelope = new byte[72];
        envelope[0] = 0x10; envelope[1] = 2;
        H("3412CDAB").CopyTo(envelope, 64);
        Payload(MelsecFxSerialOverTcpResponseParser.UnpackResponseContent(options, [], envelope), "3412CDAB");
        TestAssert.Bytes(H("10023410101210030000"), MelsecFxSerialOverTcpValueConverter.GetBytesSend(options, H("100234101210030000")));
        TestAssert.Bytes(H("100234101210030000"), MelsecFxSerialOverTcpValueConverter.GetBytesReceive(options, H("10023410101210030000")));
        Failed(MelsecFxSerialOverTcpResponseParser.UnpackResponseContent(options, [], [0x10, 2]));
        return Task.CompletedTask;
    }
}
