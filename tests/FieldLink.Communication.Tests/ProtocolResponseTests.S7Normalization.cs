using FieldLink.PlcDrivers.Siemens;
using System.Text;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    // 독립 기대값 근거: docs/siemens-s7-normalization.md. 제품 빌더로 기대 응답을 만들지 않는다.
    internal static Task S7StandardCounterAndTimerReadAsync()
    {
        foreach (string address in new[] { "C7", "T7" })
        {
            var parsed = S7DeviceAddress.ParseFrom(address, 3).Content;
            Payload(SiemensS7NetResponseParser.AnalysisReadByte([parsed],
                S7ReadReply(1, "FF0900061234ABCD5678")), "1234ABCD5678");
            byte[] request = SiemensS7NetCommandBuilder.BuildReadCommand([parsed], 0x1234).Content;
            TestAssert.Bytes(H(address[0] == 'C'
                ? "0300001F02F080320100001234000E00000401120A101C000300001C000007"
                : "0300001F02F080320100001234000E00000401120A101D000300001D000007"), request);
            Failed(SiemensS7NetResponseParser.AnalysisReadByte([parsed], S7ReadReply(1, "FF09000412345678")));
        }
        return Task.CompletedTask;
    }

    internal static Task S7StandardCounterAndTimerWriteAsync()
    {
        foreach (string address in new[] { "C7", "T7" })
        {
            var parsed = S7DeviceAddress.ParseFrom(address, 3).Content;
            Payload(SiemensS7NetCommandBuilder.BuildWriteByteCommand(parsed, H("1234ABCD5678"), 0x1234),
                address[0] == 'C'
                    ? "0300002902F080320100001234000E000A0501120A101C000300001C000007000900061234ABCD5678"
                    : "0300002902F080320100001234000E000A0501120A101D000300001D000007000900061234ABCD5678");
            Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(parsed, H("123456"), 1));
        }
        return Task.CompletedTask;
    }

    internal static Task S7WriteResponseRejectsMalformedOrWrongFunctionAsync()
    {
        byte[] good = H("0300001702F0803203000012340002000200000502FFFF");
        TestAssert.True(SiemensS7NetResponseParser.AnalysisWrite(good).IsSuccess);
        foreach (int offset in new[] { 0, 1, 3, 4, 5, 6, 7, 8, 14, 16, 19, 20 })
        {
            byte[] bad = (byte[])good.Clone();
            bad[offset] ^= 1;
            Failed(SiemensS7NetResponseParser.AnalysisWrite(bad));
        }
        Failed(SiemensS7NetResponseParser.AnalysisWrite(H("0300001602F0803203000000010002000100000500FF")));
        Failed(SiemensS7NetResponseParser.AnalysisWrite(H("0300001702F0803203000012340002000200000502FF07")), 7);
        return Task.CompletedTask;
    }

    internal static Task S7HeaderErrorsPreserveCodeWithoutItemsAsync()
    {
        byte[] error = H("0300001302F080320300001234000000008104");
        var address = S7DeviceAddress.ParseFrom("DB1.0", 1).Content;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], error), 0x8104);
        Failed(SiemensS7NetResponseParser.AnalysisWrite(error), 0x8104);
        return Task.CompletedTask;
    }

    internal static Task S7StandardStringUsesDeclaredLengthAsync()
    {
        TestAssert.Equal("AB", S7StringCodec.ParseString(SiemensPLCS.S1200, H("0502414278797A"), Encoding.ASCII));
        TestAssert.Equal("가A", S7StringCodec.ParseWideString(SiemensPLCS.S1500, H("00050002AC00004100780079")));
        TestAssert.Equal("", S7StringCodec.ParseWideString(SiemensPLCS.S1500, H("000500000078")));
        TestAssert.Equal("AB", S7StringCodec.ParseString(SiemensPLCS.S200Smart, H("0241427879"), Encoding.ASCII));
        return Task.CompletedTask;
    }

    internal static async Task S7StandardStringRejectsInvalidHeadersAsync()
    {
        foreach (byte[] header in new byte[][] { null!, [], [5], [1, 2], [255, 0] })
            Failed(S7StringCodec.GetReadLength(SiemensPLCS.S1200, header, false));
        foreach (byte[] header in new byte[][] { null!, [], [0, 5], [0, 1, 0, 2], [255, 255, 0, 0], [255, 254, 128, 0] })
            Failed(S7StringCodec.GetReadLength(SiemensPLCS.S1500, header, true));
        foreach (byte[] data in new byte[][] { [0, 5, 0, 2, 0, 65], [0, 1, 0, 2, 0, 65, 0, 66] })
            await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(S7StringCodec.ParseWideString(SiemensPLCS.S1500, data)));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(S7StringCodec.ParseString(SiemensPLCS.S1200, [5, 2, 65], Encoding.ASCII)));
    }

    internal static Task S7StringWritesRespectEncodedCapacityAsync()
    {
        Failed(S7StringCodec.BuildString(SiemensPLCS.S1200, [2, 0], "가", Encoding.UTF8));
        Failed(S7StringCodec.BuildString(SiemensPLCS.S200Smart, [], new string('A', 255), Encoding.ASCII));
        Failed(S7StringCodec.BuildString(SiemensPLCS.S1200, [0, 0], "A", Encoding.ASCII));
        Failed(S7StringCodec.BuildWideString(SiemensPLCS.S1500, [0, 0, 0, 0], "A"));
        Payload(S7StringCodec.BuildString(SiemensPLCS.S1200, [3, 0], "가", Encoding.UTF8), "0303EAB080");
        Payload(S7StringCodec.BuildWideString(SiemensPLCS.S1500, [0, 2, 0, 0], "가A"), "00020002AC000041");
        Payload(S7StringCodec.BuildString(SiemensPLCS.S1200, [0, 0], "", Encoding.ASCII), "0000");
        Payload(S7StringCodec.BuildWideString(SiemensPLCS.S1500, [0, 0, 0, 0], ""), "00000000");
        return Task.CompletedTask;
    }

    internal static Task S7TcpUntypedReadValidatesItemsAndBitsAsync()
    {
        Payload(SiemensS7ResponseParser.AnalysisReadByte(S7ReadReply(1, "FF0900061234ABCD5678")), "1234ABCD5678");
        Payload(SiemensS7ResponseParser.AnalysisReadByte(S7ReadReply(2, "FF0400081200FF0400103456")), "123456");
        foreach (string body in new[] { "", "FF0400201234", "0000FF0400101234", "FF090004001234" })
            Failed(SiemensS7ResponseParser.AnalysisReadByte(S7ReadReply(1, body)));
        Failed(SiemensS7ResponseParser.AnalysisReadByte(S7ReadReply(2, "FF0400101234")));
        Failed(SiemensS7ResponseParser.AnalysisReadByte(S7ReadReply(1, "07000000")), 7);
        foreach (string body in new[] { "", "FF03000001", "FF03000801", "FF030001", "FF04000801" })
            Failed(SiemensS7ResponseParser.AnalysisReadBit(S7ReadReply(1, body)));
        foreach (string value in new[] { "00", "01" })
            Payload(SiemensS7ResponseParser.AnalysisReadBit(S7ReadReply(1, "FF030001" + value)), value);
        byte[] bad = S7ReadReply(1, "FF0400101234");
        bad[3]++;
        Failed(SiemensS7ResponseParser.AnalysisReadByte(bad));
        return Task.CompletedTask;
    }

    internal static async Task S7NegotiatedPayloadNeverExceedsPduAsync()
    {
        byte[] setup = H("0300001B02F080320300001234000800000000F0000001000101E0");
        TestAssert.Equal(452, S7SessionCodec.ParsePayloadLimit(setup));
        setup[25] = 0; setup[26] = 128;
        TestAssert.Equal(100, S7SessionCodec.ParsePayloadLimit(setup));
        foreach (int offset in new[] { 3, 8, 14, 16, 17, 19, 20, 22, 24, 26 })
        {
            byte[] bad = (byte[])setup.Clone();
            bad[offset] = 0;
            if (offset == 16 || offset == 17 || offset == 20)
                bad[offset] = 1;
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(S7SessionCodec.ParsePayloadLimit(bad)));
        }
        await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(S7SessionCodec.ParsePayloadLimit([1, 224])));
    }

    internal static Task S7ServerResponsesEchoReferenceAndLengthsAsync()
    {
        byte[] request = H("0300001F02F080320100001234000E00000401120A10020002000184000000");
        TestAssert.Bytes(H("0300001B02F0803203000012340002000600000401FF0400101234"),
            SiemensS7ServerCommandBuilder.PackReadBack(request, H("FF0400101234").ToList()));
        request[17] = 5;
        request[18] = 2;
        byte[] write = SiemensS7ServerCommandBuilder.PackWriteBack(request, H("FFFF"));
        TestAssert.Bytes(H("0300001702F0803203000012340002000200000502FFFF"), write);
        TestAssert.True(SiemensS7NetResponseParser.AnalysisWrite(write).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task S7TruncatedTcpResponsesNeverSucceedAsync()
    {
        byte[] bytes = S7ReadReply(1, "FF0400101234");
        byte[] bit = S7ReadReply(1, "FF03000101");
        byte[] write = H("0300001602F0803203000012340002000100000501FF");
        var address = S7DeviceAddress.ParseFrom("DB1.0", 2).Content;
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes.Take(length).ToArray();
            Failed(SiemensS7NetResponseParser.AnalysisReadByte([address], truncated));
            Failed(SiemensS7ResponseParser.AnalysisReadByte(truncated));
        }
        for (int length = 0; length < bit.Length; length++)
            Failed(SiemensS7ResponseParser.AnalysisReadBit(bit.Take(length).ToArray()));
        for (int length = 0; length < write.Length; length++)
            Failed(SiemensS7NetResponseParser.AnalysisWrite(write.Take(length).ToArray()));
        bytes[0] = 0x68;
        bit[0] = 0x68;
        Failed(SiemensS7ResponseParser.AnalysisReadByte(bytes));
        Failed(SiemensS7ResponseParser.AnalysisReadBit(bit));
        return Task.CompletedTask;
    }

    internal static Task S7CounterBitOperationsAreRejectedAndMpiMappingPreservedAsync()
    {
        foreach (string address in new[] { "C7", "T7" })
        {
            Failed(SiemensS7NetCommandBuilder.BuildBitReadCommand(address, 1));
            Failed(SiemensS7NetCommandBuilder.BuildWriteBitCommand(address, true, 1));
            var parsed = S7DeviceAddress.ParseFrom(address, 3).Content;
            TestAssert.Equal(address, parsed.ToString());
            byte legacyArea = address[0] == 'C' ? (byte)0x1E : (byte)0x1F;
            // MPI의 기종별 형식은 이번 ISO/TCP 정상화의 근거로 변경하지 않는다.
            TestAssert.Equal(legacyArea, SiemensMPICommandBuilder.BuildReadCommand(2, address, 3, false).Content[31]);
            TestAssert.Equal(legacyArea, SiemensMPICommandBuilder.BuildWriteCommand(2, address, H("1234")).Content[31]);
            parsed.DataCode = legacyArea;
            Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(parsed, H("1234"), 1));
        }
        return Task.CompletedTask;
    }
}
