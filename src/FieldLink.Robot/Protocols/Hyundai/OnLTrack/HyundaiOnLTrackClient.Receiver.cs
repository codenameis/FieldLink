using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;
using FieldLink.Transport.Udp;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

public sealed partial class HyundaiOnLTrackClient
{
    private const int ReceiveErrorBackoffMilliseconds = 50;

    private static readonly TimeSpan ReceiveErrorBackoffDelay = TimeSpan.FromMilliseconds(ReceiveErrorBackoffMilliseconds);

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsOpen)
        {
            try
            {
                UdpDatagram datagram = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                await HandleIncomingDatagramAsync(datagram, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (TransportException)
            {
                if (cancellationToken.IsCancellationRequested || !IsOpen)
                {
                    break;
                }

                try
                {
                    await Task.Delay(ReceiveErrorBackoffDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (RobotException)
            {
                // 손상된 패킷 명령 바이트나 비유한 좌표는 무시하고 다음 데이터그램을 계속 대기
            }
        }
    }

    private async Task HandleIncomingDatagramAsync(UdpDatagram datagram, CancellationToken cancellationToken)
    {
        byte[] rawPayload = datagram.GetPayload();
        if (rawPayload.Length != HyundaiPacketBuilder.PacketLength)
        {
            return;
        }

        HyundaiTrackingPacket packet = HyundaiPacketParser.ParsePacket(rawPayload);
        lock (_endpointLock)
        {
            _remoteEndPoint = datagram.RemoteEndPoint;
        }

        if (packet.Command == HyundaiTrackingCommand.Start)
        {
            byte[] ack = HyundaiPacketBuilder.BuildStartAck(rawPayload);
            await _transport.SendToAsync(ack, datagram.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            PacketReceived?.Invoke(packet);
        }
        catch (Exception)
        {
            // 이벤트 구독자 예외가 백그라운드 수신 루프를 중단시키지 않도록 격리한다.
        }
    }
}
