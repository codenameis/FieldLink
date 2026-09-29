using System.Text;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Omron;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task CipReadFragmentsWriteAndStatusAsync()
    {
        byte[] frame = new byte[50];
        frame[38] = 10; frame[40] = 0xCC; frame[44] = 0xC3;
        H("3412CDAB").CopyTo(frame, 46);
        var read = AllenBradleyResponseParser.ExtractActualData(frame, true);
        TestAssert.True(read.IsSuccess, read.Message);
        TestAssert.Bytes(H("3412CDAB"), read.Content1);
        TestAssert.Equal((ushort)0xC3, read.Content2);
        TestAssert.True(!read.Content3);
        frame[42] = 6;
        var fragment = AllenBradleyResponseParser.ExtractActualData(frame, true);
        TestAssert.True(fragment.IsSuccess && fragment.Content3);
        TestAssert.Bytes(H("3412CDAB"), fragment.Content1);
        frame[42] = 5;
        Failed(AllenBradleyResponseParser.ExtractActualData(frame, true), 5);
        frame[42] = 0; frame[40] = 0xCD;
        var write = AllenBradleyResponseParser.ExtractActualData(frame, false);
        TestAssert.True(write.IsSuccess && write.Content1.Length == 0);
        frame[8] = 0x64;
        Failed(AllenBradleyResponseParser.CheckResponse(frame), 0x64);
        Failed(AllenBradleyResponseParser.ExtractActualData([], true));
        return Task.CompletedTask;
    }

    internal static Task CipMultipleServicesAndItemErrorAsync()
    {
        byte[] frame = new byte[66];
        frame[38] = 26; frame[40] = 0x8A;
        H("020006000E00CC000000C3003412CC000000C300CDAB").CopyTo(frame, 44);
        var result = AllenBradleyResponseParser.ExtractActualData(frame, true);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Bytes(H("3412CDAB"), result.Content1);
        frame[60] = 5; // 두 번째 서비스의 오류를 전체 성공으로 숨기지 않는다.
        Failed(AllenBradleyResponseParser.ExtractActualData(frame, true), 5);
        return Task.CompletedTask;
    }

    internal static Task ConnectedCipAndPcccResponsesAsync()
    {
        byte[] frame = new byte[56];
        frame[42] = 12; frame[44] = 7; frame[46] = 0xCC; frame[50] = 0xC3;
        H("3412CDAB").CopyTo(frame, 52);
        var read = ConnectedCipResponseParser.ExtractActualData(frame, true);
        TestAssert.True(read.IsSuccess && !read.Content3, read.Message);
        TestAssert.Bytes(H("3412CDAB"), read.Content1);
        TestAssert.Equal((ushort)0xC3, read.Content2);
        frame[48] = 6;
        TestAssert.True(ConnectedCipResponseParser.ExtractActualData(frame, true).Content3);
        frame[48] = 4;
        Failed(ConnectedCipResponseParser.ExtractActualData(frame, true), 4);
        byte[] pccc = new byte[65]; pccc[42] = 21; pccc[46] = 0xCB;
        H("3412CDAB").CopyTo(pccc, 61);
        var pcccRead = ConnectedCipResponseParser.ExtractActualData(pccc, true);
        TestAssert.True(pcccRead.IsSuccess, pcccRead.Message);
        TestAssert.Bytes(H("3412CDAB"), pcccRead.Content1);
        pccc[58] = 0x10;
        Failed(ConnectedCipResponseParser.ExtractActualData(pccc, true), 0x10);
        Failed(ConnectedCipResponseParser.ExtractActualData([], true));
        return Task.CompletedTask;
    }

    internal static Task SlcAndDf1DataStatusAndEscapingAsync()
    {
        byte[] slc = new byte[40]; H("3412CDAB").CopyTo(slc, 36);
        Payload(AllenBradleySLCNetResponseParser.ExtraActualContent(slc), "3412CDAB");
        Failed(AllenBradleySLCNetResponseParser.ExtraActualContent(new byte[35]));
        Payload(AllenBradleyDF1SerialResponseParser.ExtractActualData(H("100201024F00010034121010AB10030000")), "341210AB");
        Failed(AllenBradleyDF1SerialResponseParser.ExtractActualData(H("100201024F10010010030000")));
        Failed(AllenBradleyDF1SerialResponseParser.ExtractActualData(H("100201024FF00100101010030000")));
        Failed(AllenBradleyDF1SerialResponseParser.ExtractActualData([]));
        return Task.CompletedTask;
    }

    internal static Task FinsUdpTcpReadWriteAndErrorsAsync()
    {
        byte[] udp = H("C00002000D000001002A010100001234ABCD");
        Payload(OmronFinsNetResponseParser.UdpResponseValidAnalysis(udp), "1234ABCD");
        byte[] tcp = [..H("46494E530000001A0000000200000000"), ..udp];
        Payload(OmronFinsNetResponseParser.ResponseValidAnalysis(tcp), "1234ABCD");
        tcp[15] = 3;
        Failed(OmronFinsNetResponseParser.ResponseValidAnalysis(tcp), 3);
        udp[12] = 0x81; udp[13] = 1;
        Failed(OmronFinsNetResponseParser.UdpResponseValidAnalysis(udp), 0x8101);
        Payload(OmronFinsNetResponseParser.UdpResponseValidAnalysis(H("C00002000D000001002A01020000")), "");
        Failed(OmronFinsNetResponseParser.UdpResponseValidAnalysis([]));
        Failed(OmronFinsNetResponseParser.ResponseValidAnalysis(new byte[15]));
        return Task.CompletedTask;
    }

    internal static Task FinsMultipleAreaResponseAsync()
    {
        Payload(OmronFinsNetResponseParser.UdpResponseValidAnalysis(H("C00002000D000001002A0104000082123482ABCD")), "1234ABCD");
        Failed(OmronFinsNetResponseParser.UdpResponseValidAnalysis(H("C00002000D000001002A01010000")), 0);
        return Task.CompletedTask;
    }

    internal static Task HostLinkFinsAndCModeResponsesAsync()
    {
        byte[] send = A("@01FA0000000000101");
        byte[] response = A("@01FA0000000000010100001234ABCD00*\r");
        Payload(OmronHostLinkResponseParser.ResponseValidAnalysis(send, response), "1234ABCD");
        response[18] = (byte)'2';
        Failed(OmronHostLinkResponseParser.ResponseValidAnalysis(send, response));
        response[18] = (byte)'1'; response[22] = (byte)'1';
        Failed(OmronHostLinkResponseParser.ResponseValidAnalysis(send, response), 1);
        Failed(OmronHostLinkResponseParser.ResponseValidAnalysis(send, []));
        Payload(OmronHostLinkCModeResponseParser.ResponseValidAnalysis(A("@01RD001234ABCD00*\r"), true), "1234ABCD");
        Failed(OmronHostLinkCModeResponseParser.ResponseValidAnalysis(A("@01RD0100*\r"), true), 1);
        Failed(OmronHostLinkCModeResponseParser.ResponseValidAnalysis([], true));
        return Task.CompletedTask;
    }

    internal static Task OmronConnectedCipStringLengthAsync()
    {
        var context = new OmronConnectedCipOptions();
        var read = OmronConnectedCipNetResponseParser.ExtraStringContent(context, H("0300414243"), Encoding.ASCII);
        TestAssert.True(read.IsSuccess, read.Message);
        TestAssert.Equal("ABC", read.Content);
        Failed(OmronConnectedCipNetResponseParser.ExtraStringContent(context, H("0400414243"), Encoding.ASCII));
        return Task.CompletedTask;
    }
}
