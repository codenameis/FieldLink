using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.GE;
using FieldLink.Transport;
using FieldLink.Transport.Framing;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.GE;

public class GESrtpProtocolTests
{
    [Fact]
    public async Task ConnectAsync_Sends56ZeroBytes_Validates0x01Response()
    {
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());

        await protocol.ConnectAsync(default);

        Assert.Single(transport.SentFrames);
        Assert.Equal(56, transport.SentFrames[0].Length);
        Assert.All(transport.SentFrames[0], byteValue => Assert.Equal(0, byteValue));
    }

    [Fact]
    public async Task ReadAsync_M1_1Byte_MatchesTestVector_And_ExtractsShortPiggybackData()
    {
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, [0x42]));

        var request = new PlcReadRequest("M1", PlcDataType.Byte, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(0x42, result[0]);

        Assert.Equal(2, transport.SentFrames.Count);
        byte[] sentReadFrame = transport.SentFrames[1];
        Assert.Equal(56, sentReadFrame.Length);

        // 출처: docs/plc-packet-manual/ge.md §4 "M1 1바이트 읽기"
        byte[] expectedVector = [
            0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0xC0,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x04, 0x16, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        Assert.Equal(expectedVector, sentReadFrame);
    }

    [Fact]
    public async Task WriteAsync_M1_1Byte_MatchesTestVector()
    {
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, []));

        byte[] data = [0x01];
        var request = new PlcWriteRequest("M1", PlcDataType.Byte, data);
        await protocol.WriteAsync(request, default);

        Assert.Equal(2, transport.SentFrames.Count);
        byte[] sentWriteFrame = transport.SentFrames[1];
        Assert.Equal(57, sentWriteFrame.Length);

        // 출처: docs/plc-packet-manual/ge.md §4 "M1 1바이트 쓰기 (0x01)"
        byte[] expectedVector = [
            0x02, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x80,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x0E, 0x00, 0x00, 0x01, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x01, 0x07, 0x16, 0x00, 0x00, 0x01, 0x00, 0x01
        ];
        Assert.Equal(expectedVector, sentWriteFrame);
    }

    [Fact]
    public void ParseReadResponse_WhenMessageTypeIs0xD1_ThrowsPlcDeviceException()
    {
        byte[] errorResponse = new byte[56];
        errorResponse[0] = 0x03;
        errorResponse[2] = 0x01;
        errorResponse[31] = 0xD1; // 명시적 에러 응답
        errorResponse[42] = 0x05;
        errorResponse[43] = 0x00;

        var exception = Assert.Throws<PlcDeviceException>(
            () => GESrtpResponseParser.ParseReadResponse(errorResponse, expectedSequenceId: 1, expectedByteCount: 2));

        Assert.Equal(0x0005, exception.DeviceErrorCode);

        // status가 0x0000이더라도 0xD1이면 PlcDeviceException(ServiceRejected) 발생 확인
        errorResponse[42] = 0x00;
        errorResponse[43] = 0x00;
        var zeroStatusException = Assert.Throws<PlcDeviceException>(
            () => GESrtpResponseParser.ParseReadResponse(errorResponse, expectedSequenceId: 1, expectedByteCount: 2));

        Assert.Equal((int)GESrtpErrorCode.ServiceRejected, zeroStatusException.DeviceErrorCode);
    }

    [Fact]
    public async Task ReadAsync_16Bits_Unpacks2BytePiggybackInto16BooleanBytes()
    {
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());
        // 0xA5 = 0b1010_0101 (LSB first: 1,0,1,0,0,1,0,1), 0x03 = 0b0000_0011 (1,1,0,0,0,0,0,0)
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, [0xA5, 0x03]));

        var request = new PlcReadRequest("M1", PlcDataType.Boolean, 16);
        byte[] result = await protocol.ReadAsync(request, default);

        byte[] expectedBits = [1, 0, 1, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0];
        Assert.Equal(expectedBits, result);

        byte[] sentReadFrame = transport.SentFrames[1];
        Assert.Equal(0x4C, sentReadFrame[43]); // M 비트 코드
        Assert.Equal(16, sentReadFrame[46]);   // 요청 비트 개수 16
    }

    [Fact]
    public async Task ReadAsync_UnalignedBits_M4_6Bits_UnpacksFromBitOffsetInByte()
    {
        // 출처: docs/plc-packet-manual/ge.md §1.6 "M4 (WireAddress=3, bitOffsetInByte=3)에서 6비트 읽기 -> 2바이트 패킹"
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());
        // 패킹 바이트 [0x68, 0x01] -> bit 3..8 = [1, 0, 1, 1, 0, 1]
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, [0x68, 0x01]));

        var request = new PlcReadRequest("M4", PlcDataType.Boolean, 6);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal([1, 0, 1, 1, 0, 1], result);
    }

    [Fact]
    public async Task WriteAsync_Bits_PacksWithWireAddressBitOffset()
    {
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport);

        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, []));

        // M4 -> 0-based WireAddress = 3 (bitOffsetInByte = 3).
        // 6개의 비트 [1, 0, 1, 1, 0, 1] 쓰기 -> 총 비트 길이 = 3 + 6 = 9비트 (2바이트 패킹).
        // 기대 패킹 바이트: [0x68, 0x01]
        byte[] bitElements = [1, 0, 1, 1, 0, 1];
        var request = new PlcWriteRequest("M4", PlcDataType.Boolean, bitElements);
        await protocol.WriteAsync(request, default);

        byte[] sentWriteFrame = transport.SentFrames[1];
        Assert.Equal(58, sentWriteFrame.Length);
        Assert.Equal(0x4C, sentWriteFrame[51]); // M 비트 코드
        Assert.Equal(3, sentWriteFrame[52]);    // 시작 와이어 주소 3
        Assert.Equal(6, sentWriteFrame[54]);    // 쓰기 비트 수 6
        Assert.Equal(0x68, sentWriteFrame[56]); // 패킹 바이트 0
        Assert.Equal(0x01, sentWriteFrame[57]); // 패킹 바이트 1
    }

    [Fact]
    public async Task WriteAsync_UnalignedBits_WithSmallMaxWriteBytes_SplitsWithoutExceedingLimit()
    {
        // MaxWriteBytes = 2 -> 최대 비트 청크 크기 = (2 * 8) - 7 = 9비트
        var transport = new ReconnectableMockTransport();
        var protocol = new GESrtpProtocol(transport, new GESrtpOptions { MaxWriteBytes = 2 });

        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, []));
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 2, []));

        // M8(WireAddress=7, bitOffsetInByte=7)에서 10비트 쓰기 -> 첫 청크 9비트(패킹 바이트 2B), 두 번째 청크 1비트(패킹 바이트 1B)
        byte[] bitElements = [1, 0, 1, 1, 0, 1, 1, 0, 1, 1];
        var request = new PlcWriteRequest("M8", PlcDataType.Boolean, bitElements);
        await protocol.WriteAsync(request, default);

        Assert.Equal(3, transport.SentFrames.Count);
        Assert.Equal(9, transport.SentFrames[1][54]);
        Assert.Equal(1, transport.SentFrames[2][54]);
    }

    [Fact]
    public async Task ReadAsync_WhenConnectionIdChanges_Resends56ByteInitHandshake()
    {
        var transport = new ReconnectableMockTransport { ConnectionId = 10 };
        var protocol = new GESrtpProtocol(transport);

        // 첫 번째 연결(ConnectionId = 10): 초기화 1회 + 읽기 2회
        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, [0x11]));
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 2, [0x22]));

        var request = new PlcReadRequest("M1", PlcDataType.Byte, 1);
        await protocol.ReadAsync(request, default);
        await protocol.ReadAsync(request, default);

        Assert.Equal(3, transport.SentFrames.Count);

        // 재연결(ConnectionId = 11): 56바이트 초기화 핸드셰이크를 다시 전송해야 함
        transport.ConnectionId = 11;
        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 3, [0x33]));

        byte[] resultAfterReconnect = await protocol.ReadAsync(request, default);

        Assert.Equal([0x33], resultAfterReconnect);
        Assert.Equal(5, transport.SentFrames.Count);
        Assert.All(transport.SentFrames[3], byteValue => Assert.Equal(0, byteValue));
    }

    [Fact]
    public async Task ReadAndWriteAsync_Int32MultiWordChunking_And_CompletedCountOnPartialWriteFailure()
    {
        var transport = new ReconnectableMockTransport();
        // MaxWordsPerRequest = 3 -> Int32(2워드)는 청크당 최대 1개(2워드)씩 분할됨
        var options = new GESrtpOptions { MaxWordsPerRequest = 3 };
        var protocol = new GESrtpProtocol(transport, options);

        // 1. Int32 2개 분할 읽기 검증 (R1 -> WireAddress 0, R3 -> WireAddress 2)
        transport.EnqueueResponse(CreateInitResponse());
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 1, [0x01, 0x02, 0x03, 0x04]));
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 2, [0x05, 0x06, 0x07, 0x08]));

        var readRequest = new PlcReadRequest("R1", PlcDataType.Int32, 2);
        byte[] readResult = await protocol.ReadAsync(readRequest, default);

        Assert.Equal([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], readResult);
        // SentFrames[0] = Init, [1] = Read Chunk 1, [2] = Read Chunk 2
        Assert.Equal(0, transport.SentFrames[1][44]); // 첫 번째 청크 와이어 주소 0
        Assert.Equal(2, transport.SentFrames[1][46]); // 첫 번째 청크 워드 수 2
        Assert.Equal(2, transport.SentFrames[2][44]); // 두 번째 청크 와이어 주소 2
        Assert.Equal(2, transport.SentFrames[2][46]); // 두 번째 청크 워드 수 2

        // 2. Int32 2개 분할 쓰기 중 두 번째 청크에서 0xD1 거부 응답 발생 시 CompletedCount = 1 검증
        transport.EnqueueResponse(CreateShortAckResponse(sequenceId: 3, []));

        byte[] secondChunkErrorResponse = new byte[56];
        secondChunkErrorResponse[0] = 0x03;
        secondChunkErrorResponse[2] = 0x04; // sequenceId 4
        secondChunkErrorResponse[31] = 0xD1;
        secondChunkErrorResponse[42] = 0x12;
        secondChunkErrorResponse[43] = 0x00;
        transport.EnqueueResponse(secondChunkErrorResponse);

        var writeRequest = new PlcWriteRequest("R1", PlcDataType.Int32, new byte[8]);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(
            () => protocol.WriteAsync(writeRequest, default));

        Assert.Equal(0x0012, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
    }

    private static byte[] CreateInitResponse()
    {
        byte[] response = new byte[56];
        response[0] = 0x01;
        return response;
    }

    private static byte[] CreateShortAckResponse(ushort sequenceId, byte[] piggybackData)
    {
        byte[] response = new byte[56];
        response[0] = 0x03;
        response[2] = (byte)sequenceId;
        response[3] = (byte)(sequenceId >> 8);
        response[31] = 0xD4;
        Array.Copy(piggybackData, 0, response, 44, piggybackData.Length);
        return response;
    }

    private sealed class ReconnectableMockTransport : ITransport, ITransportSession
    {
        private readonly Queue<byte[]> _responses = new();
        private readonly List<byte[]> _sentFrames = new();

        public IReadOnlyList<byte[]> SentFrames => _sentFrames;

        public long ConnectionId { get; set; } = 1;

        public TransportState State => TransportState.Open;

        public CancellationToken CancellationToken => CancellationToken.None;

        public void EnqueueResponse(byte[] response) => _responses.Enqueue(response);

        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TResult> RunExclusiveAsync<TResult>(
            Func<ITransportSession, Task<TResult>> work,
            CancellationToken cancellationToken = default) => work(this);

        public Task SendAsync(byte[] frame)
        {
            _sentFrames.Add(frame);
            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("대기 중인 모의 응답이 없습니다.");
            }

            return Task.FromResult(_responses.Dequeue());
        }

        public void Dispose()
        {
        }
    }
}
