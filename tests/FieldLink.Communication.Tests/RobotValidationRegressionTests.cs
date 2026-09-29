using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Diagnostics;
using FieldLink.Robot.FANUC.Clients;
using FieldLink.Robot.FANUC.Protocols;
using FieldLink.Robot.YASKAWA.Clients;
using FieldLink.Robot.YASKAWA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotClientTests
{
    internal static async Task ReviewFanucInitializationRejectsEachStageAsync()
    {
        foreach (int failedStep in new[] { 0, 1, 5 })
        {
            using var pair = await TcpFixture.CreateAsync();
            var robot = new FanucTcpClient(pair.Client);
            var initialization = robot.InitializeAsync();
            int received = 0;
            // 실패 응답 뒤 연결이 닫힐 때까지 읽어 후속 명령 송신 여부도 확인한다.
            for (int step = 0; step < 62; step++)
            {
                byte[] request;
                try { request = await TcpFixture.ReadExactlyAsync(pair.Peer, 56).WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (EndOfStreamException) { break; }
                received++;
                await TcpFixture.ReadExactlyAsync(pair.Peer, BitConverter.ToUInt16(request, 4));
                byte[] reply = step == 0 ? FanucServerProtocol.BuildConnectReply()
                    : step == 1 ? FanucServerProtocol.BuildSessionReply() : FanucServerProtocol.BuildWriteReply(request[2], true);
                if (step == failedStep)
                {
                    if (step == 0)
                        reply[0] = 0;
                    else reply[31] = 0x99;
                }
                await TcpFixture.WriteAsync(pair.Peer, reply);
            }
            // TCP 트랜잭션은 InvalidDataException을 InvalidFrame 진단 예외로 감싼다.
            var failure = await TestAssert.FailureAsync(() => initialization, CommunicationFailure.InvalidFrame);
            TestAssert.True(failure.InnerException is InvalidDataException);
            TestAssert.Equal(failedStep, (int)failure.InnerException!.Data["InitializationStep"]!);
            TestAssert.Equal(failedStep == 0 ? 0 : 0x99, (int)failure.InnerException.Data["ErrorCode"]!);
            TestAssert.Equal(failedStep + 1, received);
            TestAssert.True(!robot.IsInitialized);
            TestAssert.Equal(ClientState.Faulted, pair.Client.State);
        }
    }

    internal static async Task ReviewYrcUdpRejectsMalformedHeadersAsync()
    {
        foreach (Action<byte[]> corrupt in new Action<byte[]>[]
        {
            b => b[0] = 0, b => b[4] = 31, b => b[6] = 1,
            b => b[8] = 0, b => b[9] = 2, b => b[10] = 0,
            b => b[12] = 1, b => b[16] = 0, b => b[24] = 0x8E
        })
        {
            using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            using var transport = new FieldLink.Communication.Udp.UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
            await transport.OpenAsync();
            var robot = new YrcHighEthernetUdpClient(transport);
            var command = robot.ExecuteAsync(new YrcEthernetRequestBuilder().BuildReset());
            var sent = await peer.ReceiveAsync();
            // 공식 HSES 공통 헤더 + Reset 서비스 0x10의 응답 0x90, 성공 상태.
            byte[] reply = TestBytes.FromHexString("5945524320000000030101000000000039393939393939399000000000000000");
            reply[11] = sent.Buffer[11];
            corrupt(reply);
            await peer.SendAsync(reply, sent.RemoteEndPoint);
            await TestAssert.FailureAsync(() => command, CommunicationFailure.ResponseRejected);
        }
    }
}
