using FieldLink.PlcDrivers.Siemens;
using Newtonsoft.Json.Linq;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task SiemensS7WordsBitsWriteAndItemErrorsAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4);
        TestAssert.True(address.IsSuccess, address.Message);
        byte[] read = H("0300001D02F0803203000000010002000800000401FF0400201234ABCD");
        Payload(SiemensS7NetResponseParser.AnalysisReadByte([address.Content], read), "1234ABCD");
        Payload(SiemensS7ResponseParser.AnalysisReadByte(read), "1234ABCD");
        read[21] = 5; read[22] = 0;
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address.Content], read), 5);
        Failed(SiemensS7NetResponseParser.AnalysisReadByte([address.Content], new byte[20]));
        Payload(SiemensS7ResponseParser.AnalysisReadBit(H("0300001A02F0803203000000010002000500000401FF03000101")), "01");
        byte[] write = H("0300001602F0803203000000010002000100000501FF");
        TestAssert.True(SiemensS7NetResponseParser.AnalysisWrite(write).IsSuccess);
        write[21] = 0x0A;
        Failed(SiemensS7NetResponseParser.AnalysisWrite(write), 0x0A);
        Failed(SiemensS7NetResponseParser.AnalysisWrite([]));
        return Task.CompletedTask;
    }

    internal static Task SiemensFetchWriteStatusAsync()
    {
        byte[] frame = H("53351001030503080000000000000000");
        TestAssert.True(SiemensFetchWriteNetResponseParser.CheckResponseContent(frame).IsSuccess);
        frame[8] = 3;
        Failed(SiemensFetchWriteNetResponseParser.CheckResponseContent(frame), 3);
        Failed(SiemensFetchWriteNetResponseParser.CheckResponseContent(new byte[8]));
        return Task.CompletedTask;
    }

    internal static Task SiemensPpiAcknowledgementLengthAndErrorsAsync()
    {
        using var ack = new MemoryStream([0xE5]);
        TestAssert.True(SiemensPPIResponseParser.CheckReceiveDataComplete(ack));
        byte[] frame = new byte[28];
        frame[0] = 0x68; frame[1] = 22; frame[2] = 22; frame[3] = 0x68;
        frame[21] = 0xFF; frame[frame.Length - 1] = 0x16;
        using var complete = new MemoryStream(frame);
        using var incomplete = new MemoryStream(TestBytes.Slice(frame, 0, trimEnd: 1));
        TestAssert.True(SiemensPPIResponseParser.CheckReceiveDataComplete(complete));
        TestAssert.True(!SiemensPPIResponseParser.CheckReceiveDataComplete(incomplete));
        TestAssert.True(SiemensPPIResponseParser.CheckResponse(frame).IsSuccess);
        frame[21] = 5;
        Failed(SiemensPPIResponseParser.CheckResponse(frame), 5);
        Failed(SiemensPPIResponseParser.CheckResponse(new byte[20]), 10000);
        return Task.CompletedTask;
    }

    internal static Task SiemensMpiHandshakeReadWriteAndErrorAsync()
    {
        TestAssert.Bytes(H("DC0000"), MpiSessionCodec.BuildHandshakeReply(H("DC0202")));
        TestAssert.Bytes(H("DC0200"), MpiSessionCodec.BuildHandshakeReply(H("DC0002")));
        TestAssert.True(MpiSessionCodec.BuildHandshakeReply(H("DC0000")) == null);
        byte[] frame = new byte[35]; frame[14] = 0xE5; frame[25] = 0xFF; frame[26] = 4;
        H("1234ABCD").CopyTo(frame, 29);
        TestAssert.True(MpiSessionCodec.CheckAcknowledgement(frame).IsSuccess);
        Payload(MpiSessionCodec.ParseRead(frame, 4), "1234ABCD");
        TestAssert.True(MpiSessionCodec.ParseWrite(frame).IsSuccess);
        frame[19] = 1; frame[14] = 0; frame[25] = 5;
        Failed(MpiSessionCodec.ParseRead(frame, 4));
        Failed(MpiSessionCodec.ParseWrite(frame));
        Failed(MpiSessionCodec.CheckAcknowledgement(frame));
        return Task.CompletedTask;
    }

    internal static Task SiemensS7PlusTypedValuesAndReturnCodeAsync()
    {
        var context = new S7PlusCodecOptions();
        (string Hex, object Expected)[] values =
        [
            ("000101", true), ("0006FF", (sbyte)-1), ("00071234", (short)0x1234),
            ("000B1234", (ushort)0x1234), ("00048100", (uint)128),
            ("000E3F800000", 1f), ("001503414243", "ABC")
        ];
        foreach (var item in values)
        {
            byte[] data = H(item.Hex); int offset = 0;
            S7Value value = SiemensS7PlusResponseParser.ExtraS7Value(context, data, ref offset, true);
            TestAssert.Equal(item.Expected, value.Value);
            TestAssert.Equal(data.Length, offset);
        }
        byte[] response = new byte[11];
        TestAssert.True(SiemensS7PlusResponseParser.CheckReturnCode(context, response).IsSuccess);
        response[10] = 1;
        Failed(SiemensS7PlusResponseParser.CheckReturnCode(context, response));
        Failed(SiemensS7PlusResponseParser.CheckReturnCode(context, new byte[10]));
        (uint Value, string Hex)[] boundaries = [(0, "00"), (127, "7F"), (128, "8100"), (16384, "818000"), (uint.MaxValue, "8FFFFFFF7F")];
        foreach (var item in boundaries)
        {
            using var stream = new MemoryStream();
            S7Object.WriteUint32(stream, item.Value);
            TestAssert.Bytes(H(item.Hex), stream.ToArray());
            int index = 0;
            TestAssert.Equal(item.Value, S7Object.GetValueUint32(H(item.Hex), ref index));
            TestAssert.Equal(item.Hex.Length / 2, index);
        }
        return Task.CompletedTask;
    }

    internal static Task SiemensWebApiRequestsReadWriteAndErrorsAsync()
    {
        JArray request = SiemensWebApiCommandBuilder.BuildWriteRawBody(7, "Data.Value", H("3412"));
        JToken expected = JToken.Parse("""[{"jsonrpc":"2.0","method":"PlcProgram.Write","id":7,"params":{"var":"Data.Value","mode":"raw","value":[52,18]}}]""");
        TestAssert.True(JToken.DeepEquals(expected, request), request.ToString());
        Payload(SiemensWebApiResponseParser.CheckReadRawResult("""[{"id":7,"result":[52,18,205,171]}]"""), "3412CDAB");
        TestAssert.True(SiemensWebApiResponseParser.CheckWriteResult("""[{"id":7,"result":true}]""").IsSuccess);
        Failed(SiemensWebApiResponseParser.CheckWriteResult("""[{"id":7,"result":false}]"""));
        Failed(SiemensWebApiResponseParser.CheckReadRawResult("""[{"id":7,"error":{"code":201,"message":"주소 오류"}}]"""), 201);
        Failed(SiemensWebApiResponseParser.CheckAndExtraJsonResult("{"));
        Failed(SiemensWebApiResponseParser.CheckAndExtraJsonResult("""[{"id":7}]"""));
        var token = SiemensWebApiLoginParser.ParseToken("""[{"id":1,"result":{"token":"test-token"}}]""");
        TestAssert.True(token.IsSuccess, token.Message);
        TestAssert.Equal("test-token", token.Content);
        Failed(SiemensWebApiLoginParser.ParseToken("[]"));
        return Task.CompletedTask;
    }
}
