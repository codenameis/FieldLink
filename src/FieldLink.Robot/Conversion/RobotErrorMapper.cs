using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport;

namespace FieldLink.Robot.Conversion;

/// <summary>프로토콜 및 전송 계층 예외를 <see cref="RobotResult"/>와 <see cref="RobotResult{T}"/>로 일괄 변환합니다.</summary>
internal static class RobotErrorMapper
{
    public static async Task<RobotResult> ExecuteAsync(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        RobotResult<bool> result = await ExecuteAsync(
            async token =>
            {
                await action(token).ConfigureAwait(false);
                return true;
            },
            timeout,
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? RobotResult.Success() : RobotResult.Failure(result.Error);
    }

    public static async Task<RobotResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            T value = await action(linkedCts.Token).ConfigureAwait(false);
            return RobotResult.Success(value);
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return RobotResult.Failure<T>(new RobotError(RobotErrorKind.Timeout, "로봇 작업 제한 시간이 초과되었습니다.")
            {
                Exception = exception
            });
        }
        catch (TransportException exception)
        {
            return RobotResult.Failure<T>(MapTransportException(exception));
        }
        catch (RobotDeviceException exception)
        {
            return RobotResult.Failure<T>(MapDeviceException(exception));
        }
        catch (RobotException exception)
        {
            return RobotResult.Failure<T>(MapRobotException(exception));
        }
    }

    public static RobotError MapTransportException(TransportException exception)
    {
        if (exception.InnerException is TimeoutException)
        {
            return new RobotError(RobotErrorKind.Timeout, exception.Message)
            {
                Exception = exception
            };
        }

        RobotErrorKind kind = exception.Failure switch
        {
            TransportFailure.ConnectFailed => RobotErrorKind.ConnectionFailed,
            TransportFailure.ConnectionClosed => RobotErrorKind.ConnectionLost,
            TransportFailure.InvalidFrame => RobotErrorKind.InvalidResponse,
            _ => RobotErrorKind.ConnectionLost
        };

        return new RobotError(kind, exception.Message)
        {
            Exception = exception
        };
    }

    private static RobotError MapDeviceException(RobotDeviceException exception) =>
        new(RobotErrorKind.DeviceError, exception.Message, exception.DeviceErrorCode, exception.GetRawFrame())
        {
            Exception = exception
        };

    private static RobotError MapRobotException(RobotException exception) =>
        new(exception.Kind, exception.Message, exception.ProtocolErrorCode, exception.GetRawFrame())
        {
            Exception = exception
        };
}
