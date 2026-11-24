using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Hyundai.OnLTrack;
using FieldLink.Robot.Tests.Fakes;
using FieldLink.Transport;
using FieldLink.Transport.Udp;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Hyundai.OnLTrack;

public sealed class HyundaiOnLTrackTests
{
    [Fact]
    public void PacketBuilder_SpecVectors_MatchExactBytes()
    {
        // 출처: docs/specs/robot/05-hyundai-udp.md §9.2 Vector 1 MoveX(10.0)
        var moveXPacket = new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            State = 2,
            Count = 1,
            Data = new[] { 10.0, 0, 0, 0, 0, 0 }
        };

        byte[] actualMoveX = HyundaiPacketBuilder.BuildPacket(moveXPacket);
        byte[] expectedPrefix = FakeTransport.ParseHex(
            "50 00 00 00 02 00 00 00 01 00 00 00 00 00 00 00 " +
            "7B 14 AE 47 E1 7A 84 3F");

        Assert.Equal(64, actualMoveX.Length);
        Assert.Equal(expectedPrefix, actualMoveX[..24]);
        Assert.All(actualMoveX[24..], b => Assert.Equal(0, b));

        // 출처: docs/specs/robot/05-hyundai-udp.md §9.2 Vector 2 RotateZ(180.0)
        var rotateZPacket = new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            State = 2,
            Count = 1,
            Data = new[] { 0, 0, 0, 0, 0, 180.0 }
        };

        byte[] actualRotateZ = HyundaiPacketBuilder.BuildPacket(rotateZPacket);
        Assert.Equal(FakeTransport.ParseHex("18 2D 44 54 FB 21 09 40"), actualRotateZ[56..64]);
    }

    [Fact]
    public void PacketBuilderAndParser_TruncatesCharDummyToThreeBytesAndRoundtripsUnits()
    {
        var packet = new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            CharDummy = "ABCDEF",
            State = 2,
            Count = 42,
            Data = new[] { 125.5, -50.0, 1000.0, 45.0, -90.0, 180.0 }
        };

        byte[] raw = HyundaiPacketBuilder.BuildPacket(packet);
        HyundaiTrackingPacket decoded = HyundaiPacketParser.ParsePacket(raw);

        Assert.Equal("ABC", decoded.CharDummy);
        Assert.Equal(2, decoded.State);
        Assert.Equal(42, decoded.Count);
        Assert.Equal(125.5, decoded.Data[0], 6);
        Assert.Equal(-50.0, decoded.Data[1], 6);
        Assert.Equal(1000.0, decoded.Data[2], 6);
        Assert.Equal(45.0, decoded.Data[3], 6);
        Assert.Equal(-90.0, decoded.Data[4], 6);
        Assert.Equal(180.0, decoded.Data[5], 6);

        Assert.Throws<RobotException>(() => HyundaiPacketParser.ParsePacket(new byte[63]));
    }

    [Fact]
    public async Task Client_PreConnectGuardsAutoStartAckIncrementAndReceiveEvent_WorkEndToEnd()
    {
        var listener = new FakeUdpListenerTransport();
        using var client = new HyundaiOnLTrackClient(listener);

        // 서버 시작 전 쓰기 거부
        RobotResult notStarted = await client.MoveXAsync(10.0);
        Assert.False(notStarted.IsSuccess);
        Assert.Equal(RobotErrorKind.ConnectionFailed, notStarted.Error.Kind);

        await client.OpenAsync();

        // 로봇 패킷 수신 전 쓰기 거부
        RobotResult notConnected = await client.MoveXAsync(10.0);
        Assert.False(notConnected.IsSuccess);
        Assert.Equal(RobotErrorKind.ConnectionFailed, notConnected.Error.Kind);

        var ackReceivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processPacketTcs = new TaskCompletionSource<HyundaiTrackingPacket>(TaskCreationOptions.RunContinuationsAsynchronously);

        listener.DatagramSent += (bytes, _) =>
        {
            if (bytes[0] == (byte)'S')
            {
                ackReceivedTcs.TrySetResult(true);
            }
        };
        client.PacketReceived += pkt =>
        {
            if (pkt.Command == HyundaiTrackingCommand.Process)
            {
                processPacketTcs.TrySetResult(pkt);
            }
        };

        var robotEndpoint = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 41000);
        byte[] startFrame = new byte[64];
        startFrame[0] = (byte)'S';
        startFrame[12] = 0x77; // IntDummy preserved in Start ACK

        listener.EnqueueInbound(startFrame, robotEndpoint);
        await Task.WhenAny(ackReceivedTcs.Task, Task.Delay(1000));
        Assert.True(ackReceivedTcs.Task.IsCompletedSuccessfully);

        Assert.Equal(robotEndpoint, client.RemoteEndPoint);
        byte[] ackBytes = listener.SentDatagrams[0].Payload;
        Assert.Equal((byte)'S', ackBytes[0]);
        Assert.Equal(1, BitConverter.ToInt32(ackBytes, 4));
        Assert.Equal(0, BitConverter.ToInt32(ackBytes, 8));
        Assert.Equal(0x77, ackBytes[12]);

        RobotResult moveResult = await client.MoveXAsync(10.0);
        Assert.True(moveResult.IsSuccess);
        Assert.Equal(2, listener.SentDatagrams.Count);
        Assert.Equal(robotEndpoint, listener.SentDatagrams[1].RemoteEndPoint);

        byte[] processInbound = HyundaiPacketBuilder.BuildPacket(new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            State = 5,
            Count = 9,
            Data = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }
        });
        listener.EnqueueInbound(processInbound, robotEndpoint);

        await Task.WhenAny(processPacketTcs.Task, Task.Delay(1000));
        Assert.True(processPacketTcs.Task.IsCompletedSuccessfully);
        HyundaiTrackingPacket observed = await processPacketTcs.Task;
        Assert.Equal(9, observed.Count);
        Assert.Equal(1.0, observed.Data[0], 6);

        await client.CloseAsync();
    }

    [Fact]
    public async Task ValidationBackoffAndDisposeRace_RejectNaNAndPreventSpinLoopOrSemaphoreLeaks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HyundaiTrackingPacket { Data = new[] { double.NaN, 0, 0, 0, 0, 0 } });
        Assert.Throws<ArgumentOutOfRangeException>(() => HyundaiPacketBuilder.BuildPacket(new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            Data = new[] { 0, double.PositiveInfinity, 0, 0, 0, 0 }
        }));

        byte[] nanWire = HyundaiPacketBuilder.BuildPacket(new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            Data = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 }
        });
        RobotBinaryBuffer.WriteDoubleLittleEndian(nanWire, 16, double.NaN);
        RobotException parseEx = Assert.Throws<RobotException>(() => HyundaiPacketParser.ParsePacket(nanWire));
        Assert.Equal(RobotErrorKind.InvalidResponse, parseEx.Kind);

        using var faultingListener = new FaultingUdpListenerTransport();
        using var backoffClient = new HyundaiOnLTrackClient(faultingListener);
        await backoffClient.OpenAsync();
        await Task.Delay(160);
        await backoffClient.CloseAsync();

        // 50ms 백오프가 작동해 160ms 동안 무제한 100% CPU 스핀이 발생하지 않음을 검증한다.
        Assert.InRange(faultingListener.ReceiveAttempts, 1, 8);

        var listener = new FakeUdpListenerTransport();
        var raceClient = new HyundaiOnLTrackClient(listener);
        await raceClient.OpenAsync();
        Task closeTask = raceClient.CloseAsync();
        raceClient.Dispose();
        await closeTask;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => raceClient.OpenAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => raceClient.MoveXAsync(1.0));
    }

    private sealed class FaultingUdpListenerTransport : IUdpListenerTransport
    {
        public int ReceiveAttempts;

        public TransportState State { get; private set; } = TransportState.Closed;

        public int BoundPort => 7127;

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            State = TransportState.Open;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            State = TransportState.Closed;
            return Task.CompletedTask;
        }

        public Task<UdpDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ReceiveAttempts);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<UdpDatagram>(new TransportException(TransportFailure.ReceiveFailed, "Simulated UDP fault"));
        }

        public Task SendToAsync(byte[] payload, IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose()
        {
            State = TransportState.Disposed;
        }
    }
}
