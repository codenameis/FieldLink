using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.WebSocket;

public sealed partial class WebSocketTransport
{
    private async Task CloseSocketSafelyAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        bool sendLockAcquired = false;
        try
        {
            sendLockAcquired = await _sendLock
                .WaitAsync(CloseSendLockWaitMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            if (!sendLockAcquired)
            {
                return;
            }

            if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            {
                return;
            }

            bool receiveLockAcquired = _receiveLock.Wait(0);
            if (receiveLockAcquired)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, NormalClosureDescription, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    _receiveLock.Release();
                }
            }
            else
            {
                // 진행 중인 ReceiveAsync와 동시 Receive 충돌을 피하기 위해 CloseOutputAsync로 송신 측만 닫는다.
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, NormalClosureDescription, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // 종료 핸드셰이크 실패 시 강제 정리를 계속 진행
        }
        finally
        {
            if (sendLockAcquired)
            {
                _sendLock.Release();
            }

            AbortAndDisposeSocket(socket);
        }
    }

    private static void CancelSafely(CancellationTokenSource? cancellationSource)
    {
        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // OpenAsync finally와 경합하여 이미 해제된 경우 무시
        }
    }

    private static void AbortAndDisposeSocket(ClientWebSocket? socket)
    {
        try
        {
            socket?.Abort();
            socket?.Dispose();
        }
        catch
        {
            // 소켓 정리 중 예외는 무시
        }
    }
}
