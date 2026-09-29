using System.Net;
using System.Net.Sockets;
using System.Text;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Beckhoff;
using FieldLink.PlcDrivers.GE;
using FieldLink.PlcDrivers.Knx;
using FieldLink.PlcDrivers.LSIS;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.Yokogawa;
using SocketUdpClient = System.Net.Sockets.UdpClient;
using UdpClient = FieldLink.Communication.Udp.UdpClient;

namespace FieldLink.Communication.Tests;

// 로컬 상대가 고정 요청을 검증하고 고정 응답을 보낸다. PLC 세션·메모리 전체를 흉내 내지는 않는다.
// 요청과 응답의 기대 바이트는 제품 빌더의 실행 결과로 만들지 않는다.
internal static class ProtocolLoopbackTests
{
    internal static async Task AdsLargeFrameKeepsFollowingFrameOverTcpAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var parser = new AdsTcpFrameRules();
        var boundary = new HeaderLengthFrame(6, header =>
        {
            byte[] headerBytes = header.ToArray();
            return 6 + parser.GetBodyLength(headerBytes);
        });
        byte[] normal = H("00002C0000000708090A0B0C01800102030405065303020005000C000000000000007856341200000000040000003412CDAB");
        byte[] large = new byte[12006];
        TestBytes.Slice(normal, 0, 46).CopyTo(large, 0);
        H("E02E0000").CopyTo(large, 2);  // AMS 헤더와 데이터 12000바이트
        H("C02E0000").CopyTo(large, 26); // ADS 데이터 11968바이트
        H("B82E0000").CopyTo(large, 42); // 읽은 메모리 11960바이트
        for (int i = 46; i < large.Length; i++) large[i] = (byte)i;
        Task pending = pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            byte[] first = await transaction.ReceiveAsync(boundary);
            TestAssert.Equal(large.Length, first.Length);
            TestAssert.Bytes(large, first);
            TestAssert.Bytes(TestBytes.Slice(large, 46), Success(BeckhoffAdsNetResponseParser.UnpackResponseContent(new AdsFrameOptions(), [], first)));
            TestAssert.Bytes(normal, await transaction.ReceiveAsync(boundary));
            return true;
        }, TcpFixture.Timeout, cancellationToken: deadline.Token);
        await SendAsync(pair.Peer, TestBytes.Slice(large, 0, 3), deadline.Token);
        await SendAsync(pair.Peer, [..TestBytes.Slice(large, 3), ..normal], deadline.Token);
        await pending;
    }

    internal static async Task AdsAndFinsOversizedFramesFailBeforeBodyAsync()
    {
        foreach (IProtocolFrameRules parser in new IProtocolFrameRules[] { new AdsTcpFrameRules(), new FinsTcpFrameRules() })
        {
            using var pair = await TcpFixture.CreateAsync(maximumFrameLength: 1024);
            var boundary = new HeaderLengthFrame(parser.HeaderLength, header =>
            {
                byte[] headerBytes = header.ToArray();
                return parser.HeaderLength + parser.GetBodyLength(headerBytes);
            });
            Task pending = pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(boundary), TcpFixture.Timeout);
            byte[] header = parser is AdsTcpFrameRules ? H("0000E02E0000") : H("46494E5300002EE00000000200000000");
            await TcpFixture.WriteAsync(pair.Peer, header);
            await TestAssert.FailureAsync(() => pending, FieldLink.Communication.Diagnostics.CommunicationFailure.MessageTooLarge);
        }
    }

    internal static async Task SiemensS7ReadOverTcpAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4);
        TestAssert.True(address.IsSuccess, address.Message);
        byte[] request = Success(SiemensS7NetCommandBuilder.BuildReadCommand([address.Content], 1));
        await TcpExchangeAsync(request, H("0300001F02F080320100000001000E00000401120A10020004000184000000"),
            H("0300001D02F0803203000000010002000800000401FF0400201234ABCD"), new S7FrameRules(),
            response => TestAssert.Bytes(H("1234ABCD"), Success(SiemensS7NetResponseParser.AnalysisReadByte([address.Content], response))));
    }

    internal static async Task OmronFinsReadOverTcpAsync()
    {
        byte[] core = FinsReadCore();
        byte[] request = OmronFinsNetCommandBuilder.PackCommand(new FinsTcpFrameOptions { DA1 = 1, SA1 = 13, SID = 42 }, core);
        await TcpExchangeAsync(request, H("46494E530000001A0000000200000000800002000100000D002A0101820064000002"),
            H("46494E530000001A0000000200000000C00002000D000001002A010100001234ABCD"), new FinsTcpFrameRules(),
            response => TestAssert.Bytes(H("1234ABCD"), Success(OmronFinsNetResponseParser.ResponseValidAnalysis(response))));
    }

    internal static async Task BeckhoffAdsReadOverTcpAsync()
    {
        var options = new AdsFrameOptions { targetAMSNetId = H("0102030405065303"), sourceAMSNetId = H("0708090A0B0C0180"), InvokeId = 0x12345678 };
        byte[] request = BeckhoffAdsNetCommandBuilder.PackCommandWithHeader(options, Success(AdsCommandBuilder.BuildReadCommand("M100", 4, false)));
        await TcpExchangeAsync(request,
            H("00002C00000001020304050653030708090A0B0C0180020004000C0000000000000078563412204000006400000004000000"),
            H("00002C0000000708090A0B0C01800102030405065303020005000C000000000000007856341200000000040000003412CDAB"),
            new AdsTcpFrameRules(), response => TestAssert.Bytes(H("3412CDAB"),
                Success(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, request, response))));
    }

    internal static async Task AllenBradleyCipReadOverTcpAsync()
    {
        byte[] cip = AllenBradleyCommandBuilder.PackRequsetRead("Tag", 2);
        TestAssert.Bytes(H("4C039103546167000200"), cip);
        byte[] body = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4],
            AllenBradleyCommandBuilder.PackCommandSingleService(cip));
        byte[] request = AllenBradleyCommandBuilder.PackRequestHeader(0x6F, 0x12345678, body);
        await TcpExchangeAsync(request,
            H("6F001A007856341200000000000000000000000000000000000000000A00020000000000B2000A004C039103546167000200"),
            H("6F001A007856341200000000000000000000000000000000000000000000020000000000B2000A00CC000000C3003412CDAB"),
            new EtherNetIpFrameRules(), response =>
            {
                var result = AllenBradleyResponseParser.ExtractActualData(response, true);
                TestAssert.True(result.IsSuccess, result.Message);
                TestAssert.Bytes(H("3412CDAB"), result.Content1);
                TestAssert.Equal((ushort)0xC3, result.Content2);
            });
    }

    internal static async Task LsFastEnetReadOverTcpAsync()
    {
        var options = new FastEnetFrameOptions();
        byte[] core = Success(LSFastEnetCommandBuilder.BuildReadByteCommand("D100", 4));
        byte[] request = LSFastEnetCommandBuilder.PackCommandWithHeader(options, core);
        // D100은 바이트 주소 %DB200이다. 헤더의 앞 19바이트 합은 0x343 → 검증 바이트 43.
        await TcpExchangeAsync(request,
            H("4C5349532D58475400000000A033000012000343540014000000010006002544423230300400"),
            H("4C5349532D58475400000101A01100001000001E5500140000000000010004003412CDAB"),
            new LsisFastEnetFrameRules(), response =>
            {
                TestAssert.Bytes(H("3412CDAB"), Success(LSFastEnetResponseParser.ExtractActualData(options, response)));
                TestAssert.Equal(LSCpuStatus.RUN, options.LSCpuStatus);
            });
    }

    internal static async Task MelsecMcAsciiReadOverTcpAsync()
    {
        var address = McDeviceAddress.ParseMelsecFrom("D100", 2, true);
        TestAssert.True(address.IsSuccess, address.Message);
        byte[] request = McAsciiCommandBuilder.PackMcCommand(new McFrameOptions(), McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(address.Content, false));
        await TcpExchangeAsync(request, A("500000FF03FF000018001004010000D*0001000002"),
            A("D00000FF03FF00000C00001234ABCD"), new MelsecQnA3EAsciiFrameRules(), response =>
            {
                var status = McAsciiResponseParser.CheckResponseContent(response);
                TestAssert.True(status.IsSuccess, status.Message);
                TestAssert.Bytes(H("3412CDAB"), McAsciiResponseParser.ExtractActualDataHelper(TestBytes.Slice(response, 22), false));
            });
    }

    internal static async Task GeSrtpReadOverTcpAsync()
    {
        byte[] response = new byte[56];
        response[0] = 3; response[2] = 0x34; response[3] = 0x12; response[31] = 0xD4;
        H("3412").CopyTo(response, 44); // 짧은 SRTP 응답의 6바이트 슬롯 중 요청한 2바이트
        await TcpExchangeAsync(Success(GeCommandBuilder.BuildReadCommand(0x1234, "R1", 2, false)),
            H("02003412000000000001000000000000000100000000000000000000000006C000000000100E000001010408000001000000000000000000"),
            response, new GeSrtpFrameRules(), data => TestAssert.Bytes(H("341200000000"), Success(GeResponseParser.ExtraResponseContent(data))));
    }

    internal static async Task YokogawaLinkReadOverTcpAsync()
    {
        var requests = YokogawaLinkTcpCommandBuilder.BuildReadCommand(1, "D100", 2, false);
        TestAssert.True(requests.IsSuccess, requests.Message);
        TestAssert.Equal(1, requests.Content.Count);
        await TcpExchangeAsync(requests.Content[0], H("110100080004000000640002"),
            H("910000041234ABCD"), new YokogawaLinkBinaryFrameRules(),
            data => TestAssert.Bytes(H("1234ABCD"), Success(YokogawaLinkTcpResponseParser.CheckContent(data))));
    }

    internal static async Task FinsUdpIgnoresPreviousTransactionAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        await client.OpenAsync(TcpFixture.Timeout, deadline.Token);
        byte[] request = OmronFinsUdpCommandBuilder.PackCommand(new FinsUdpFrameOptions { DA1 = 1, SA1 = 13, SID = 42 }, FinsReadCore());
        var parser = new FinsUdpFrameRules();
        Task<byte[]> pending = client.ExchangeAsync(request, TcpFixture.Timeout,
            data => parser.ClassifyResponse(request, data),
            deadline.Token);
        var sent = await peer.ReceiveAsync(deadline.Token);
        TestAssert.Bytes(H("800002000100000D002A0101820064000002"), sent.Buffer);
        await peer.SendAsync(H("C00002000D000001002901010000FFFFEEEE"), sent.RemoteEndPoint, deadline.Token);
        await Task.Delay(20, deadline.Token);
        TestAssert.True(!pending.IsCompleted, "이전 SID의 데이터가 현재 요청을 완료했습니다.");
        byte[] response = H("C00002000D000001002A010100001234ABCD");
        await peer.SendAsync(response, sent.RemoteEndPoint, deadline.Token);
        byte[] received = await pending;
        TestAssert.Bytes(response, received);
        TestAssert.Bytes(H("1234ABCD"), Success(OmronFinsNetResponseParser.UdpResponseValidAnalysis(received)));
    }

    internal static async Task KnxMultiByteIndicationOverUdpAsync()
    {
        using var peer = new SocketUdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        await client.OpenAsync(TcpFixture.Timeout, deadline.Token);
        byte[] request = KnxCommandBuilder.BuildRead(5, 7, 0x1200);
        Task<byte[]> pending = client.ExchangeAsync(request, TcpFixture.Timeout, cancellationToken: deadline.Token);
        var sent = await peer.ReceiveAsync(deadline.Token);
        TestAssert.Bytes(H("061004200015040507001100BCE000001200010000"), sent.Buffer);
        byte[] indication = H("061004200017040507002900BCE0000012000300801234");
        await peer.SendAsync(indication, sent.RemoteEndPoint, deadline.Token);
        byte[] received = await pending;
        TestAssert.Bytes(indication, received);
        var result = KnxResponseParser.Parse(5, true, received);
        TestAssert.Equal((short)0x1200, result.Address);
        TestAssert.Bytes(H("1234"), result.Data);
        TestAssert.Bytes(H("06100421000A04050700"), result.Reply);
    }

    private static byte[] FinsReadCore()
    {
        var result = OmronFinsNetCommandBuilder.BuildReadCommand(OmronPlcType.CSCJ, "D100", 2, false);
        TestAssert.True(result.IsSuccess, result.Message);
        TestAssert.Equal(1, result.Content.Count);
        TestAssert.Bytes(H("0101820064000002"), result.Content[0]);
        return result.Content[0];
    }

    private static async Task TcpExchangeAsync(byte[] request, byte[] expectedRequest, byte[] response,
        IProtocolFrameRules parser, Action<byte[]> check)
    {
        TestAssert.Bytes(expectedRequest, request);
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        var boundary = new HeaderLengthFrame(parser.HeaderLength, header =>
        {
            byte[] headerBytes = header.ToArray();
            TestAssert.True(parser.IsHeaderValid(headerBytes, request), parser.GetType().Name);
            return parser.HeaderLength + parser.GetBodyLength(headerBytes, request);
        });
        TestAssert.Equal(response.Length, boundary.GetFrameLength(new ArraySegment<byte>(response))!.Value);
        Task<(byte[] First, byte[] Second)> pending = pair.Client.ExecuteTransactionAsync(async transaction =>
        {
            await transaction.SendAsync(request);
            return (await transaction.ReceiveAsync(boundary), await transaction.ReceiveAsync(boundary));
        }, TcpFixture.Timeout, cancellationToken: deadline.Token);
        byte[] sent = new byte[request.Length];
        int count = 0;
        while (count < sent.Length)
        {
            int read = await pair.Peer.ReceiveAsync(new ArraySegment<byte>(sent, count, sent.Length - count), SocketFlags.None, deadline.Token);
            if (read == 0)
                throw new EndOfStreamException("요청 수신 도중 연결이 닫혔습니다.");
            count += read;
        }
        TestAssert.Bytes(expectedRequest, sent);
        await SendAsync(pair.Peer, TestBytes.Slice(response, 0, 1), deadline.Token);
        await Task.Delay(15, deadline.Token);
        TestAssert.True(!pending.IsCompleted, "일부 헤더만으로 수신이 완료되었습니다.");
        // 첫 응답의 나머지와 두 번째 응답을 한 번에 전달하여 누적 버퍼의 프레임 분리를 검사한다.
        await SendAsync(pair.Peer, [..TestBytes.Slice(response, 1), ..response], deadline.Token);
        var received = await pending;
        foreach (byte[] frame in new[] { received.First, received.Second })
        {
            TestAssert.Bytes(response, frame);
            check(frame);
        }
    }

    private static async Task SendAsync(Socket socket, byte[] data, CancellationToken token)
    {
        int sent = 0;
        while (sent < data.Length)
        {
            int count = await socket.SendAsync(new ArraySegment<byte>(data, sent, data.Length - sent), SocketFlags.None, token);
            if (count == 0)
                throw new EndOfStreamException("응답 송신 도중 연결이 닫혔습니다.");
            sent += count;
        }
    }
    private static byte[] Success(OperationResult<byte[]> result)
    {
        TestAssert.True(result.IsSuccess, result.Message);
        return result.Content;
    }
    private static byte[] H(string hex) => TestBytes.FromHexString(hex);
    private static byte[] A(string text) => Encoding.ASCII.GetBytes(text);
}
