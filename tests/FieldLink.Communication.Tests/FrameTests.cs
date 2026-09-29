using FieldLink.Communication.Diagnostics;
using FieldLink.Communication;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;
using System.Net;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tests;

internal static class FrameTests
{
    // 참고 소스 기준 커밋의 헤더 규칙을 독립적인 수신 fixture에 적용한다.
    internal static readonly IFrameBoundary S7 = new HeaderLengthFrame(4, header =>
    {
        if (header.Array![header.Offset] != 3 || header.Array[header.Offset + 1] != 0)
            throw new InvalidDataException("Invalid S7 TPKT header");
        return (header.Array[header.Offset + 2] << 8) | header.Array[header.Offset + 3];
    });

    internal static async Task HeaderProtocolsAsync()
    {
        var cases = new (IFrameBoundary Decoder, byte[] Frame)[]
        {
            (S7, [3, 0, 0, 7, 0xAA, 0xBB, 0xCC]),
            (new HeaderLengthFrame(9, bytes => 9 + bytes.Array![bytes.Offset + 7] + (bytes.Array[bytes.Offset + 8] << 8)),
                [0xD0, 0, 0, 0xFF, 0xFF, 3, 0, 2, 0, 0, 0]),
            (new HeaderLengthFrame(24, bytes => 24 + bytes.Array![bytes.Offset + 2] + (bytes.Array[bytes.Offset + 3] << 8)),
                [0x6F, 0, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xCC, 0xDD])
        };
        foreach (var item in cases)
        {
            using var pair = await TcpFixture.CreateAsync();
            Task<byte[]> response = pair.Client.ExchangeAsync([0x11], item.Decoder, TcpFixture.Timeout);
            TestAssert.Bytes([0x11], await TcpFixture.ReadExactlyAsync(pair.Peer, 1));
            foreach (byte value in item.Frame)
            {
                await TcpFixture.WriteAsync(pair.Peer, [value]);
                await Task.Delay(2);
            }
            TestAssert.Bytes(item.Frame, await response);
        }
    }

    internal static async Task CoalescedFramesAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [3, 0, 0, 5, 1, 3, 0, 0, 6, 2, 3]);
        var frames = await pair.Client.ExecuteTransactionAsync(async transaction =>
            (await transaction.ReceiveAsync(S7), await transaction.ReceiveAsync(S7)), TcpFixture.Timeout);
        TestAssert.Bytes([3, 0, 0, 5, 1], frames.Item1);
        TestAssert.Bytes([3, 0, 0, 6, 2, 3], frames.Item2);
    }

    internal static async Task DelimiterAndTrailerAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var decoder = new DelimitedFrame([13, 10], 2);
        Task<byte[]> receive = pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(decoder), TcpFixture.Timeout);
        await TcpFixture.WriteAsync(pair.Peer, [65, 13]);
        await Task.Delay(20);
        await TcpFixture.WriteAsync(pair.Peer, [10, 7, 8, 66, 13, 10, 9, 10]);
        TestAssert.Bytes([65, 13, 10, 7, 8], await receive);
        TestAssert.Bytes([66, 13, 10, 9, 10], await pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(decoder), TcpFixture.Timeout));
    }

    internal static async Task CustomBoundaryAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [2, 0xAA, 0xBB]);
        TestAssert.Bytes([2, 0xAA, 0xBB], await pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(new CountedBytesBoundary()), TcpFixture.Timeout));
    }

    private sealed class CountedBytesBoundary : IFrameBoundary
    {
        public int? GetFrameLength(ArraySegment<byte> data) => data.Count == 0 ? null : data.Array![data.Offset] + 1;
    }

    internal static async Task OversizedHeaderAsync()
    {
        using var pair = await TcpFixture.CreateAsync(8);
        await TcpFixture.WriteAsync(pair.Peer, [3, 0, 0, 9]);
        var error = await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(S7), TcpFixture.Timeout), CommunicationFailure.MessageTooLarge);
        TestAssert.Equal(CommunicationStage.Validating, error.Stage);
        TestAssert.True(!pair.Client.IsConnected);
    }

    internal static async Task MissingDelimiterLimitAsync()
    {
        using var pair = await TcpFixture.CreateAsync(4);
        await TcpFixture.WriteAsync(pair.Peer, [1, 2, 3, 4]);
        await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(new DelimitedFrame([13])), TcpFixture.Timeout), CommunicationFailure.MessageTooLarge);
    }

    internal static async Task InvalidHeaderAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [4, 0, 0, 4]);
        var error = await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(S7), TcpFixture.Timeout), CommunicationFailure.InvalidFrame);
        TestAssert.True(error.InnerException is InvalidDataException);
    }

    internal static async Task HeaderTooShortLengthAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        await TcpFixture.WriteAsync(pair.Peer, [3, 0, 0, 2]);
        await TestAssert.FailureAsync(() => pair.Client.ExecuteTransactionAsync(transaction => transaction.ReceiveAsync(S7), TcpFixture.Timeout), CommunicationFailure.InvalidFrame);
    }

    internal static async Task InvalidArgumentsAsync()
    {
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(new FixedLengthFrame(0)));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(new DelimitedFrame([])));
        using var client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 1), new FixedLengthFrame(1));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.OpenAsync(TimeSpan.Zero));
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync([1], TcpFixture.Timeout));
    }
}
