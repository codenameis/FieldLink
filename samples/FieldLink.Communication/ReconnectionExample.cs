using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Diagnostics;

namespace FieldLink.Samples;

// 애플리케이션 정책의 예제다. 라이브러리가 이 메서드를 자동으로 호출하지 않는다.
// 하나의 제어 흐름에서 호출하고, 실패한 장치 명령은 별도로 사용자에게 전달한다.
public static class ReconnectionExample
{
    public static async Task ReconnectAsync(SessionDevice device, int maximumAttempts, TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        if (retryDelay < TimeSpan.Zero || retryDelay.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(retryDelay));

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await device.OpenAsync(cancellationToken).ConfigureAwait(false);
                return; // 연결만 복구한다. Read/Write나 초기화 명령을 재실행하지 않는다.
            }
            catch (CommunicationException error) when (attempt < maximumAttempts &&
                (error.Failure == CommunicationFailure.ConnectionClosed ||
                 error.Failure == CommunicationFailure.SocketError ||
                 error.Failure == CommunicationFailure.Timeout))
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
