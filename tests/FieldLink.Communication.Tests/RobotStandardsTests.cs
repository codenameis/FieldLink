using System.Net;
using System.Text;
using FieldLink.Robot.ABB.Clients;
using FieldLink.Robot.YASKAWA.Protocols;
using FieldLink.Robot.YAMAHA.Clients;
using FieldLink.Robot.YAMAHA.Protocols;
using FieldLink.Robot.KUKA.Clients;
using FieldLink.Robot.KUKA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    internal static Task StandardsYrcAdditionalStatusUsesOfficialMeaningAsync()
    {
        // Yaskawa HSES Error and Status Code Tables, 2024-03-01.
        TestAssert.Equal("서보 OFF 상태", YrcHighEthernetProtocol.GetErrorText(0x1F, 0x2070));
        TestAssert.Equal("위치 데이터가 없습니다.", YrcHighEthernetProtocol.GetErrorText(0x1F, 0x4130));
        TestAssert.Equal("응답 데이터 크기가 소프트웨어 제한을 벗어났습니다.",
            YrcHighEthernetProtocol.GetErrorText(0x1F, 0xA101));
        byte[] response = TestBytes.FromHexString(
            "59455243200000000301012A000000003939393939393939811F020070200000");
        var failure = YrcHighEthernetProtocol.CheckResponseContent(response);
        TestAssert.True(!failure.IsSuccess);
        TestAssert.Equal(0x1F, failure.ErrorCode);
        TestAssert.Equal("서보 OFF 상태", failure.Message);
        return Task.CompletedTask;
    }

    internal static Task StandardsKukaProxyRequiresCompleteDeclaredResponseAsync()
    {
        // IMTS KUKAVARPROXY Answer Message Format: 4-byte header, mode,
        // 2-byte value length, value, three-byte tail 00 01 01 on success.
        byte[] valid = TestBytes.FromHexString("1234000700000158000101");
        TestAssert.Bytes([(byte)'X'], KukaVarProxyProtocol.ExtractActualData(valid).Content);
        foreach (byte[] malformed in new[]
        {
            TestBytes.FromHexString("123400050000015801"),
            TestBytes.FromHexString("1234000800000158000101"),
            TestBytes.FromHexString("1234000700000258000101"),
            TestBytes.FromHexString("1234000700000158090101"),
            TestBytes.FromHexString("1234000700000158000001")
        })
            TestAssert.True(!KukaVarProxyProtocol.ExtractActualData(malformed).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task StandardsYamahaLoadUsesAsciiCommandPrefixAsync()
    {
        // Yamaha RCX340-PRO_E_V1.20 chapter 12, section 2.1, p.12-9.
        TestAssert.Bytes(Encoding.ASCII.GetBytes("@ LOAD <MAIN>, T1\r\n"),
            YamahaRcxProtocol.BuildLoad("MAIN", 1));
        return Task.CompletedTask;
    }

    internal static Task StandardsYrcStatusRequestMatchesOfficialHeaderAsync()
    {
        // Yaskawa HSES Controller Status Reading, 2024-03-12: command 0x72,
        // instance 1, attribute 0, service 1, no request payload.
        byte[] expected = TestBytes.FromHexString(
            "59455243200000000301002A0000000039393939393939397200010000010000");
        TestAssert.Bytes(expected, new YrcEthernetRequestBuilder().BuildReadStats().Build(42));
        return Task.CompletedTask;
    }

    internal static async Task StandardsYrcRejectsPayloadLengthOverflowAsync()
    {
        // HSES Data Length has two bytes. Never silently wrap 65536 to zero.
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            YrcHighEthernetProtocol.BuildCommand(1, 0, 0x72, 1, 0, 1, new byte[65536])));
        byte[] maximum = YrcHighEthernetProtocol.BuildCommand(1, 0, 0x72, 1, 0, 1, new byte[65535]);
        TestAssert.Equal((byte)255, maximum[6]);
        TestAssert.Equal((byte)255, maximum[7]);
    }

    internal static Task StandardsYrcShortStatusResponseReturnsFailureAsync()
    {
        TestAssert.True(!YrcHighEthernetProtocol.CheckResponseContent(null!).IsSuccess);
        for (int length = 0; length < 32; length++)
            TestAssert.True(!YrcHighEthernetProtocol.CheckResponseContent(new byte[length]).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task StandardsYrcStatusResponseHasTwoFourByteFieldsAsync()
    {
        // Published response data is exactly two 4-byte bitfields, not a prefix
        // of an arbitrarily long successful payload.
        var request = new YrcEthernetRequestBuilder().BuildReadStats();
        byte[] valid = TestBytes.FromHexString(
            "59455243200008000301012A00000000393939393939393981000000000000008000000040000000");
        var result = request.ParseResponse(valid);
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(16, result.Content.Length);
        TestAssert.True(result.Content[7]);
        TestAssert.True(result.Content[14]);
        foreach (int length in new[] { 0, 7, 9, 12 })
        {
            byte[] malformed = new byte[32 + length];
            Array.Copy(valid, malformed, Math.Min(valid.Length, malformed.Length));
            malformed[6] = (byte)length;
            TestAssert.True(!request.ParseResponse(malformed).IsSuccess,
                "Status reply accepted a payload whose length is not eight bytes.");
        }
        return Task.CompletedTask;
    }
}

internal static partial class RobotClientTests
{
    internal static async Task StandardsYamahaJogRejectsBeforeSendingAsync()
    {
        // RCX340-PRO_E_V1.20 section 4.4, p.12-40: JOG requires RUN/END
        // and 0x16 every 200ms. This API has no motion lifetime contract.
        using var pair = await TcpFixture.CreateAsync();
        var robot = new YamahaRcxTcpClient(pair.Client);
        var jog = robot.JogXYAsync(1);
        var firstByte = TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        Task completed = await Task.WhenAny(jog, firstByte).WaitAsync(TimeSpan.FromSeconds(3));
        TestAssert.True(ReferenceEquals(completed, jog),
            "Unsupported JOGXY started transmitting a movement command.");
        var result = await jog;
        TestAssert.True(!result.IsSuccess);
        TestAssert.True(result.Message.Contains("200ms") && result.Message.Contains("RUN/END"));

        // The first transmitted bytes must belong to the next valid command.
        var reset = robot.ResetAsync();
        byte[] expected = Encoding.ASCII.GetBytes("@ RESET \r\n");
        byte[] actual = new byte[expected.Length];
        actual[0] = (await firstByte)[0];
        (await TcpFixture.ReadExactlyAsync(pair.Peer, expected.Length - 1)).CopyTo(actual, 1);
        TestAssert.Bytes(expected, actual);
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("OK\r\n"));
        TestAssert.True((await reset).IsSuccess);
    }

    internal static async Task StandardsKukaProxyCorrelatesResponseIdAndModeAsync()
    {
        foreach (string response in new[] { "0001000700000158000101", "0000000701000158000101" })
        {
            using var pair = await TcpFixture.CreateAsync();
            var robot = new KukaVarProxyTcpClient(pair.Client);
            var read = robot.ReadStringAsync("A");
            TestAssert.Bytes(TestBytes.FromHexString("0000000400000141"),
                await TcpFixture.ReadExactlyAsync(pair.Peer, 8));
            await TcpFixture.WriteAsync(pair.Peer, TestBytes.FromHexString(response));
            TestAssert.True(!(await read).IsSuccess,
                "Accepted an answer to a different KUKAVARPROXY request.");
        }
    }

    internal static async Task StandardsYamahaMultiLineReplyDoesNotResendCommandAsync()
    {
        // Yamaha RCX340-PRO_E_V1.20 section 3.3: one @?MOTOR produces
        // the value and OK lines. The next command must not see a duplicate.
        using var pair = await TcpFixture.CreateAsync();
        var robot = new YamahaRcxTcpClient(pair.Client);
        var status = robot.ReadMotorStatusAsync();
        byte[] query = Encoding.ASCII.GetBytes("@?MOTOR \r\n");
        TestAssert.Bytes(query, await TcpFixture.ReadExactlyAsync(pair.Peer, query.Length));
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("2\r\nOK\r\n"));
        TestAssert.Equal(2, (await status).Content);

        var reset = robot.ResetAsync();
        byte[] next = Encoding.ASCII.GetBytes("@ RESET \r\n");
        TestAssert.Bytes(next, await TcpFixture.ReadExactlyAsync(pair.Peer, next.Length));
        await TcpFixture.WriteAsync(pair.Peer, Encoding.ASCII.GetBytes("OK\r\n"));
        TestAssert.True((await reset).IsSuccess);
    }

    internal static async Task StandardsAbbPostUsesRwsFormMediaTypeAsync()
    {
        using var handler = new StandardsAbbFormHandler();
        using var http = new HttpClient(handler);
        var robot = new AbbHttpClient(http, new Uri("http://localhost:8123/"));
        const string form = "lvalue=1&text=A%2BB";
        var result = await robot.WriteAsync("url=/rw/iosystem/signals/Virtual/DUNIT/sig1?action=set", form);
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal("application/x-www-form-urlencoded", handler.MediaType!);
        TestAssert.Equal(form, handler.Body!);
        TestAssert.Equal("/rw/iosystem/signals/Virtual/DUNIT/sig1?action=set", handler.Path!);
    }

    private sealed class StandardsAbbFormHandler : HttpMessageHandler
    {
        internal string? MediaType;
        internal string? Body;
        internal string? Path;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            MediaType = request.Content!.Headers.ContentType!.MediaType;
            Body = await request.Content.ReadAsStringAsync();
            Path = request.RequestUri!.PathAndQuery;
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8)
            };
        }
    }
}
