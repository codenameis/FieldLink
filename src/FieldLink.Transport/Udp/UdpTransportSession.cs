using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;

namespace FieldLink.Transport.Udp;

internal sealed class UdpTransportSession : ITransportSession
{
    private readonly UdpTransport _owner;
    private readonly UdpClient _client;
    private readonly TimeSpan _receiveTimeout;
    private bool _hasSent;

    public UdpTransportSession(
        UdpTransport owner,
        long connectionId,
        UdpClient client,
        TimeSpan receiveTimeout,
        CancellationToken cancellationToken)
    {
        _owner = owner;
        ConnectionId = connectionId;
        _client = client;
        _receiveTimeout = receiveTimeout;
        CancellationToken = cancellationToken;
    }

    public long ConnectionId { get; }

    public CancellationToken CancellationToken { get; }

    public async Task SendAsync(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        CancellationToken.ThrowIfCancellationRequested();
        _owner.DiscardStalePendingReceiveAndDrain(_client);

        try
        {
            await _client.SendAsync(frame, frame.Length).ConfigureAwait(false);
            _hasSent = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "UDP 소켓이 닫혀 전송할 수 없습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(
                TransportFailure.SendFailed,
                "UDP 데이터그램 전송에 실패했습니다.",
                isRequestSent: true,
                innerException: exception);
        }
    }

    public async Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
    {
        if (decoder is null)
        {
            throw new ArgumentNullException(nameof(decoder));
        }

        CancellationToken.ThrowIfCancellationRequested();

        UdpReceiveResult receiveResult = await ReceiveDatagramAsync().ConfigureAwait(false);
        return DecodeDatagram(decoder, receiveResult.Buffer);
    }

    private async Task<UdpReceiveResult> ReceiveDatagramAsync()
    {
        using CancellationTokenSource timeoutSource = new(_receiveTimeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            CancellationToken,
            timeoutSource.Token);

        Task<UdpReceiveResult> receiveTask = _owner.GetOrStartReceiveTask(_client, _hasSent);
        var cancelCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            using (linkedSource.Token.Register(
                static state =>
                {
                    if (state is TaskCompletionSource<bool> completion)
                    {
                        completion.TrySetResult(true);
                    }
                },
                cancelCompletion))
            {
                Task completedTask = await Task.WhenAny(receiveTask, cancelCompletion.Task).ConfigureAwait(false);
                if (completedTask != receiveTask)
                {
                    TransportTaskWait.ObserveFaultSafely(receiveTask);
                    CancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"UDP 수신 제한 시간({_receiveTimeout.TotalMilliseconds}ms)이 초과되었습니다.");
                }
            }

            _owner.ClearPendingReceiveTask(receiveTask);
            return await receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TransportException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            _owner.ClearPendingReceiveTask(receiveTask);
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "UDP 소켓이 닫혀 수신할 수 없습니다.",
                isRequestSent: _hasSent,
                innerException: exception);
        }
        catch (Exception exception)
        {
            if (receiveTask.IsCompleted)
            {
                _owner.ClearPendingReceiveTask(receiveTask);
            }

            throw new TransportException(
                TransportFailure.ReceiveFailed,
                "UDP 데이터그램 수신에 실패했습니다.",
                isRequestSent: _hasSent,
                innerException: exception);
        }
    }

    private byte[] DecodeDatagram(IFrameDecoder decoder, byte[] buffer)
    {
        if (!decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength))
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                "수신된 UDP 데이터그램이 완전한 프레임 조건을 만족하지 못했습니다.",
                isRequestSent: _hasSent);
        }

        if (frameLength <= 0 || frameLength > buffer.Length)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"디코더가 유효하지 않은 프레임 길이({frameLength})를 반환했습니다.",
                isRequestSent: _hasSent);
        }

        if (frameLength == buffer.Length)
        {
            return buffer;
        }

        byte[] frame = new byte[frameLength];
        Array.Copy(buffer, 0, frame, 0, frameLength);
        return frame;
    }
}
