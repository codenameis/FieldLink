using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;

namespace FieldLink.Communication.Tests;

/// <summary>실제 루프백 TCP로 QnA 호환 3E 바이너리 요청·응답을 교환합니다.</summary>
internal static class MelsecQnA3ETests
{
    // 고정 기대 프레임: 원본 McBinaryHelper의 기본 경로·감시 타이머와
    // MelsecMcServer.ReadByCommand의 0401/0000, D=A8, 24비트 주소 형식.
    // 50 00 | 00 FF FF 03 00 | 0C 00 | 0A 00 | 01 04 | 00 00 | 64 00 00 | A8 | 32 00
    private const string ReadD100FiftyWordsHex = "500000FFFF03000C000A0001040000640000A83200";

    internal static async Task ReadD100ThroughD149Async()
    {
        using var pair = await TcpFixture.CreateAsync(); // 서버 Listen → 클라이언트 Open → 서버 Accept
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        ushort[] memory = Enumerable.Range(0, 200).Select(address => (ushort)address).ToArray();

        Task<MelsecQnA3EPlc.Exchange> server = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        Task<ReadExchange> client = ReadWordsAsync(pair.Client, "D100", 50, deadline.Token);
        await Task.WhenAll(server, client);

        ReadExchange read = await client;
        MelsecQnA3EPlc.Exchange served = await server;
        TestAssert.Bytes(TestBytes.FromHexString(ReadD100FiftyWordsHex), served.Request);
        TestAssert.Bytes(read.Request, served.Request);
        TestAssert.Bytes(served.Response, read.Response);
        TestAssert.Equal(21, read.Request.Length);
        TestAssert.Equal(111, read.Response.Length); // 9바이트 헤더 + 종료 코드 2 + 데이터 100
        TestAssert.Bytes(TestBytes.FromHexString("D00000FFFF030066000000"), TestBytes.Slice(read.Response, 0, 11));
        TestAssert.True(read.Result.IsSuccess, read.Result.Message);
        TestAssert.Equal(50, read.Result.Content.Length);
        for (int index = 0; index < 50; index++)
            TestAssert.Equal((ushort)(100 + index), read.Result.Content[index]);

        Console.WriteLine($"QnA3E 가상 PLC: {pair.Listener.LocalEndpoint} / D100부터 50워드 연속 읽기");
        Console.WriteLine($"TX ({read.Request.Length} bytes): {Hex(read.Request)}");
        Console.WriteLine($"RX ({read.Response.Length} bytes): {Hex(read.Response)}");
        for (int offset = 0; offset < 50; offset += 10)
            Console.WriteLine($"D{100 + offset}~D{109 + offset}: {string.Join(", ", read.Result.Content.Skip(offset).Take(10))}");
    }

    internal static async Task ReadUsesRequestedAddressAndServerMemoryAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        ushort[] memory = new ushort[200];
        memory[120] = 0x1234;
        memory[121] = 0xABCD;
        memory[122] = 0xFFFF;

        Task<MelsecQnA3EPlc.Exchange> server = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        Task<ReadExchange> client = ReadWordsAsync(pair.Client, "D120", 3, deadline.Token);
        await Task.WhenAll(server, client);
        ReadExchange read = await client;

        // 고정 응답을 되돌려주는 모형이 아니라 요청 주소의 메모리를 읽는지 확인한다.
        TestAssert.True(read.Result.IsSuccess, read.Result.Message);
        TestAssert.Equal(3, read.Result.Content.Length);
        TestAssert.Equal((ushort)0x1234, read.Result.Content[0]);
        TestAssert.Equal((ushort)0xABCD, read.Result.Content[1]);
        TestAssert.Equal((ushort)0xFFFF, read.Result.Content[2]);
        TestAssert.Bytes(TestBytes.FromHexString("D00000FFFF0300080000003412CDABFFFF"), read.Response);
    }

    internal static async Task DeviceAddressErrorRemainsFailureAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TcpFixture.Timeout);
        ushort[] memory = new ushort[200]; // D0~D199만 있으므로 D190부터 50개는 범위 초과

        Task<MelsecQnA3EPlc.Exchange> server = MelsecQnA3EPlc.ServeOnceAsync(pair.Peer, memory, deadline.Token);
        Task<ReadExchange> client = ReadWordsAsync(pair.Client, "D190", 50, deadline.Token);
        await Task.WhenAll(server, client);
        ReadExchange read = await client;

        TestAssert.Bytes(TestBytes.FromHexString("D00000FFFF0300020056C0"), read.Response);
        TestAssert.True(!read.Result.IsSuccess);
        TestAssert.Equal(0xC056, read.Result.ErrorCode);
        TestAssert.True(read.Result.Message.Contains("최대 주소"));
        TestAssert.True(pair.Client.IsConnected, "PLC 종료 코드 오류 자체가 TCP 연결 오류는 아닙니다.");
    }

    private static async Task<ReadExchange> ReadWordsAsync(ITcpClient client, string address, ushort count, CancellationToken token)
    {
        // 1. 주소 파싱 → 읽기 명령 본문 → 3E 헤더 포장.
        OperationResult<McDeviceAddress> parsed = McDeviceAddress.ParseMelsecFrom(address, count, false);
        TestAssert.True(parsed.IsSuccess, parsed.Message);
        byte[] core = McBinaryCommandBuilder.BuildReadMcCoreCommand(parsed.Content, false);
        byte[] request = McBinaryCommandBuilder.PackMcCommand(new McFrameOptions(), core);

        // 2. 9바이트 헤더의 길이 필드로 한 프레임의 끝을 결정한다.
        var boundary = new HeaderLengthFrame(9, header =>
        {
            byte[] headerBytes = header.ToArray();
            var frame = new MelsecQnA3EBinaryFrameRules();
            if (!frame.IsHeaderValid(headerBytes))
                throw new InvalidDataException("QnA3E 응답의 서브헤더가 D0 00이 아닙니다.");
            return frame.HeaderLength + frame.GetBodyLength(headerBytes);
        });
        byte[] response = await client.ExchangeAsync(request, boundary, TcpFixture.Timeout, cancellationToken: token);

        // 3. 종료 코드 확인 → 11바이트 응답 헤더 제거 → 워드 데이터 해석.
        OperationResult status = McBinaryResponseParser.CheckResponseContentHelper(response);
        if (!status.IsSuccess)
            return new ReadExchange(OperationResult.CreateFailedResult<ushort[]>(status), request, response);

        byte[] data = McBinaryResponseParser.ExtractActualDataHelper(TestBytes.Slice(response, 11), false);
        TestAssert.Equal(count * 2, data.Length);
        ushort[] words = new ProtocolValueConverter().ReadUInt16(data, 0, count);
        return new ReadExchange(OperationResult.CreateSuccessResult(words), request, response);
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace('-', ' ');
    private sealed class ReadExchange
    {
        internal ReadExchange(OperationResult<ushort[]> result, byte[] request, byte[] response)
        { Result = result; Request = request; Response = response; }
        internal OperationResult<ushort[]> Result { get; }
        internal byte[] Request { get; }
        internal byte[] Response { get; }
    }
}
