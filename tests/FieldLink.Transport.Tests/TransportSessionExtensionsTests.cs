using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Transport.Tests;

public sealed class TransportSessionExtensionsTests
{
    [Fact]
    public async Task ExchangeAsync_SendsRequestThenReceivesResponse()
    {
        var session = new RecordingSession(response: new byte[] { 0xAA });

        byte[] response = await session.ExchangeAsync(new byte[] { 0x01 }, new WholeBufferDecoder());

        Assert.Equal(new byte[] { 0x01 }, session.SentFrame);
        Assert.Equal(new byte[] { 0xAA }, response);
    }

    private sealed class RecordingSession : ITransportSession
    {
        private readonly byte[] _response;

        public RecordingSession(byte[] response)
        {
            _response = response;
        }

        public byte[]? SentFrame { get; private set; }

        public long ConnectionId => 1;

        public CancellationToken CancellationToken => CancellationToken.None;

        public Task SendAsync(byte[] frame)
        {
            SentFrame = frame;
            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(IFrameDecoder decoder) => Task.FromResult(_response);
    }

    private sealed class WholeBufferDecoder : IFrameDecoder
    {
        public bool TryGetFrameLength(ArraySegment<byte> received, out int frameLength)
        {
            frameLength = received.Count;
            return received.Count > 0;
        }
    }
}
