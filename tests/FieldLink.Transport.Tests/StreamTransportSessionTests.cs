using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Transport.Tests;

public sealed class StreamTransportSessionTests
{
    [Fact]
    public async Task SendAsync_WritesBytesToStream()
    {
        using var memoryStream = new MemoryStream();
        var session = new StreamTransportSession(1, memoryStream, CancellationToken.None);
        byte[] payload = new byte[] { 0x01, 0x02, 0x03 };

        await session.SendAsync(payload);

        Assert.Equal(payload, memoryStream.ToArray());
    }

    [Fact]
    public async Task ReceiveAsync_ExtractsFrameAndPreservesRemainingBytes()
    {
        // 5바이트 프레임 2개가 연속으로 들어있는 스트림
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x11, 0x12, 0x13, 0x14, 0x15 };
        using var memoryStream = new MemoryStream(data);
        var session = new StreamTransportSession(1, memoryStream, CancellationToken.None);
        var decoder = new FixedLengthFrameDecoder(5);

        byte[] firstFrame = await session.ReceiveAsync(decoder);
        byte[] secondFrame = await session.ReceiveAsync(decoder);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, firstFrame);
        Assert.Equal(new byte[] { 0x11, 0x12, 0x13, 0x14, 0x15 }, secondFrame);
    }

    [Fact]
    public async Task SendAsync_ClearsStaleBufferedBytesBeforeNewRequest()
    {
        // 첫 번째 프레임(5바이트) 뒤에 이전 요청의 잔여 바이트(3바이트)가 남아있는 상태에서
        // 새 요청(SendAsync)이 호출되면 내부 버퍼가 비워지고 onBeforeSend 콜백이 호출되어야 함
        using var stream = new MemoryStream();
        stream.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0xAA, 0xBB, 0xCC }, 0, 8);
        stream.Position = 0;

        bool beforeSendCalled = false;
        var session = new StreamTransportSession(
            1,
            stream,
            CancellationToken.None,
            onBeforeSend: () => beforeSendCalled = true);
        var decoder = new FixedLengthFrameDecoder(5);

        byte[] firstFrame = await session.ReceiveAsync(decoder);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, firstFrame);

        // 이제 스트림 끝에 새 응답 5바이트만 추가하고 SendAsync를 호출하면 이전 잔여 3바이트(0xAA, 0xBB, 0xCC)는 폐기됨
        await session.SendAsync(new byte[] { 0xFF });
        Assert.True(beforeSendCalled);
    }

    [Fact]
    public async Task ReceiveAsync_WhenStreamClosesBeforeCompleteFrame_ThrowsConnectionClosed()
    {
        // 5바이트 프레임 디코더인데 스트림에 3바이트만 존재
        var data = new byte[] { 0x01, 0x02, 0x03 };
        using var memoryStream = new MemoryStream(data);
        var session = new StreamTransportSession(1, memoryStream, CancellationToken.None);
        var decoder = new FixedLengthFrameDecoder(5);

        TransportException exception = await Assert.ThrowsAsync<TransportException>(
            () => session.ReceiveAsync(decoder));

        Assert.Equal(TransportFailure.ConnectionClosed, exception.Failure);
    }
}
