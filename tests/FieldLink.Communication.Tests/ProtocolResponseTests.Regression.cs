using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Siemens;
using System.Text;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task SmartWideStringKeepsExistingWriteFormatAsync()
    {
        // 사용자가 유지하기로 한 사용자 정의 형식: 바이트 수 1바이트 + UTF-16BE.
        foreach (var sample in new[] { ("", "00"), ("AB", "0400410042"), ("가", "02AC00"), ("가A", "04AC000041"), ("😀", "04D83DDE00") })
        {
            byte[] expected = H(sample.Item2);
            Payload(S7StringCodec.BuildWideString(SiemensPLCS.S200Smart, [], sample.Item1), sample.Item2);
            TestAssert.Equal(sample.Item1, S7StringCodec.ParseWideString(SiemensPLCS.S200Smart, expected));
            var length = S7StringCodec.GetReadLength(SiemensPLCS.S200Smart, TestBytes.Slice(expected, 0, 1), true);
            TestAssert.True(length.IsSuccess, length.Message);
            TestAssert.Equal((ushort)expected.Length, length.Content);
            TestAssert.Equal(sample.Item1, S7StringCodec.ParseWideString(SiemensPLCS.S200Smart, [..expected, 0xEE, 0xEE]));
        }
        return Task.CompletedTask;
    }

    internal static Task SmartWideStringRejectsUnrepresentableLengthAsync()
    {
        string maximum = new string('가', 127);
        var result = S7StringCodec.BuildWideString(SiemensPLCS.S200Smart, [], maximum);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal(255, result.Content.Length);
        TestAssert.Equal((byte)254, result.Content[0]);
        TestAssert.Equal(maximum, S7StringCodec.ParseWideString(SiemensPLCS.S200Smart, result.Content));
        TestAssert.Equal((ushort)255, S7StringCodec.GetReadLength(SiemensPLCS.S200Smart, [254], true).Content);
        Failed(S7StringCodec.BuildWideString(SiemensPLCS.S200Smart, [], maximum + "가"));
        Failed(S7StringCodec.BuildString(SiemensPLCS.S200Smart, [], maximum + "가", Encoding.Unicode));
        return Task.CompletedTask;
    }

    internal static async Task SmartWideStringRejectsTruncatedOrOddPayloadAsync()
    {
        foreach (byte[] header in new byte[][] { [], [1], [255] })
            Failed(S7StringCodec.GetReadLength(SiemensPLCS.S200Smart, header, true));
        foreach (byte[] data in new byte[][] { [], [1, 0x41], [2, 0xAC] })
            await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(S7StringCodec.ParseWideString(SiemensPLCS.S200Smart, data)));
        // 일반 STRING은 원래의 단일 바이트 문자 형식을 그대로 사용한다.
        Payload(S7StringCodec.BuildString(SiemensPLCS.S200Smart, [], "ABC", Encoding.ASCII), "03414243");
        TestAssert.Equal((ushort)4, S7StringCodec.GetReadLength(SiemensPLCS.S200Smart, [3], false).Content);
        TestAssert.Equal("ABC", S7StringCodec.ParseString(SiemensPLCS.S200Smart, H("03414243"), Encoding.ASCII));
    }

    internal static Task S7MissingItemsNeverBecomeZeroValuesAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4).Content;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], H("0300001502F0803203000000010002000000000401")));
        var second = S7DeviceAddress.ParseFrom("DB1.4", 2).Content;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address, second],
            S7ReadReply(2, "FF0400201234ABCD")));
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], S7ReadReply(1, "FF0400201234")));
        return Task.CompletedTask;
    }

    internal static Task S7UnknownItemStatusRemainsFailureAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4).Content;
        foreach (byte status in new byte[] { 1, 3, 5, 6, 7, 10, 0xFE })
            Failed(SiemensS7NetResponseParser.AnalysisReadByte([address],
                S7ReadReply(1, $"{status:X2}000000")), status);
        return Task.CompletedTask;
    }

    internal static Task S7DeclaredLengthsAndTrailingDataAreCheckedAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4).Content;
        foreach (string body in new[] { "FF0400101234ABCD", "FF0400281234ABCD00", "FF0400201234ABCDFF", "0100FF0400201234ABCD" })
            Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], S7ReadReply(1, body)));
        byte[] reply = S7ReadReply(1, "FF0400201234ABCD");
        reply[16]--;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], reply));
        reply = S7ReadReply(1, "FF0400201234ABCD");
        reply[3]--;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], reply));
        reply = S7ReadReply(1, "FF0400201234ABCD");
        reply[17] = 0x81; reply[18] = 4;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], reply), 0x8104);
        return Task.CompletedTask;
    }

    internal static Task S7MultipleItemsRespectPaddingAsync()
    {
        var first = S7DeviceAddress.ParseFrom("DB1.0", 1).Content;
        var second = S7DeviceAddress.ParseFrom("DB1.2", 2).Content;
        Payload(SiemensS7NetResponseParser.AnalysisReadByte([first, second],
            S7ReadReply(2, "FF0400081200FF0400103456")), "123456");
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([first, second],
            S7ReadReply(2, "FF04000812FF0400103456")));
        var third = S7DeviceAddress.ParseFrom("DB1.4", 1).Content;
        Payload(SiemensS7NetResponseParser.AnalysisReadByte([second, third],
            S7ReadReply(2, "FF0400101234FF04000856")), "123456");
        return Task.CompletedTask;
    }

    internal static Task S7CounterAndTimerRecordsPreserveTheirValuesAsync()
    {
        // 기존 1E/1F는 S7-200 계열이다. 일반 S7 C/T(1C/1D)의 2바이트 값과 구분한다.
        var counters = new S7DeviceAddress { DataCode = 0x1E, AddressStart = 0, Length = 2 };
        var word = S7DeviceAddress.ParseFrom("DB1.0", 2).Content;
        Payload(SiemensS7NetResponseParser.AnalysisReadByte([counters, word],
            S7ReadReply(2, "FF09000600123400ABCDFF0400105678")), "1234ABCD5678");
        var timers = new S7DeviceAddress { DataCode = 0x1F, AddressStart = 0, Length = 3 };
        Payload(SiemensS7NetResponseParser.AnalysisReadByte([timers],
            S7ReadReply(1, "FF09000F0000001234000000ABCD0000005678")), "1234ABCD5678");
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([counters],
            S7ReadReply(1, "FF090003001234")));
        return Task.CompletedTask;
    }

    private static byte[] S7ReadReply(byte items, string data)
    {
        byte[] body = H(data);
        byte[] reply = [..H("0300000002F0803203000000010002000000000400"), ..body];
        reply[2] = (byte)(reply.Length >> 8); reply[3] = (byte)reply.Length;
        reply[15] = (byte)(body.Length >> 8); reply[16] = (byte)body.Length;
        reply[20] = items;
        return reply;
    }

    internal static Task FinsCommandErrorsRemainFailuresWithCpuFlagsAsync()
    {
        // W342 §5-1-3: 명령 오류와 CPU 상태 비트는 별개다. 2101은 쓰기 불가다.
        foreach (ushort endCode in new ushort[] { 0x2101, 0x1103, 0x0401, 0x0001, 0x2141, 0x2181, 0x8000 })
        foreach (byte command in new byte[] { 1, 2 })
        {
            byte[] udp = H("C00002000D000001002A01020000");
            udp[11] = command;
            udp[12] = (byte)(endCode >> 8);
            udp[13] = (byte)endCode;
            // 데이터가 붙어 있어도 오류 응답을 성공으로 승격하면 안 된다.
            if (command == 1)
                udp = [..udp, 0x12, 0x34];
            Failed(OmronFinsNetResponseParser.UdpResponseValidAnalysis(udp), endCode);
            byte[] tcp = [..H("46494E53000000000000000200000000"), ..udp];
            tcp[7] = (byte)(tcp.Length - 8);
            Failed(OmronFinsNetResponseParser.ResponseValidAnalysis(tcp), endCode);
        }
        return Task.CompletedTask;
    }

    internal static Task FinsNormalCompletionPreservesCpuStatusAsync()
    {
        // W342의 0040 예: 배터리 오류가 있어도 명령 자체는 정상 완료될 수 있다.
        foreach (ushort endCode in new ushort[] { 0x0000, 0x0040, 0x0080, 0x00C0 })
        foreach (byte command in new byte[] { 1, 2 })
        {
            byte[] udp = H("C00002000D000001002A01020000");
            udp[11] = command;
            udp[13] = (byte)endCode;
            if (command == 1)
                udp = [..udp, 0x12, 0x34];
            var result = OmronFinsNetResponseParser.UdpResponseValidAnalysis(udp);
            Payload(result, command == 1 ? "1234" : "");
            TestAssert.Equal((int)endCode, result.ErrorCode);
            byte[] tcp = [..H("46494E53000000000000000200000000"), ..udp];
            tcp[7] = (byte)(tcp.Length - 8);
            var tcpResult = OmronFinsNetResponseParser.ResponseValidAnalysis(tcp);
            Payload(tcpResult, command == 1 ? "1234" : "");
            TestAssert.Equal((int)endCode, tcpResult.ErrorCode);
        }
        return Task.CompletedTask;
    }
}
