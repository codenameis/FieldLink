using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;

namespace FieldLink.OpcUa.Internal;

internal readonly struct OpcUaDeadline
{
    private const long MinimumDelayTicks = 1L;
    private const double MinimumTimeoutHintMilliseconds = 1.0;

    private readonly long _deadlineTimestamp;

    private OpcUaDeadline(long deadlineTimestamp)
    {
        _deadlineTimestamp = deadlineTimestamp;
    }

    public static OpcUaDeadline StartAfter(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "제한 시간은 0보다 커야 합니다.");
        }

        long now = Stopwatch.GetTimestamp();
        double rawTicks = Math.Ceiling(timeout.TotalSeconds * Stopwatch.Frequency);
        long maxDelta = long.MaxValue - now;
        long deltaTicks = rawTicks >= maxDelta
            ? maxDelta
            : Math.Max(MinimumDelayTicks, (long)rawTicks);
        return new OpcUaDeadline(now + deltaTicks);
    }

    public TimeSpan Remaining
    {
        get
        {
            long remainingTicks = _deadlineTimestamp - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return TimeSpan.Zero;
            }

            double seconds = (double)remainingTicks / Stopwatch.Frequency;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public bool IsExpired => _deadlineTimestamp - Stopwatch.GetTimestamp() <= 0;

    public TimeSpan ThrowIfExpiredOrCanceled(CancellationToken callerToken, string operationName)
    {
        callerToken.ThrowIfCancellationRequested();

        TimeSpan remaining = Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"{operationName} 작업의 전체 제한 시간이 초과되었습니다.");
        }

        return remaining;
    }

    public uint GetOperationTimeoutHintMilliseconds(CancellationToken callerToken, string operationName)
    {
        TimeSpan remaining = ThrowIfExpiredOrCanceled(callerToken, operationName);
        double clampedMilliseconds = Math.Min(int.MaxValue, Math.Max(MinimumTimeoutHintMilliseconds, Math.Ceiling(remaining.TotalMilliseconds)));
        return (uint)clampedMilliseconds;
    }

    public static void ObserveOrphanedFault(Task task)
    {
        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                _ = task.Exception;
            }

            return;
        }

        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        string operationName,
        CancellationToken callerToken)
    {
        TimeSpan remaining = ThrowIfExpiredOrCanceled(callerToken, operationName);

        using CancellationTokenSource timeoutSource = new(remaining);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            timeoutSource.Token);

        Task<TResult>? operationTask = null;
        try
        {
            operationTask = operation(linkedSource.Token);
            Task delayTask = Task.Delay(Timeout.InfiniteTimeSpan, linkedSource.Token);

            Task completedTask = await Task.WhenAny(operationTask, delayTask).ConfigureAwait(false);
            if (completedTask == operationTask)
            {
                return await operationTask.ConfigureAwait(false);
            }

            // 클라이언트 타임아웃·취소가 먼저 성립해도 뒤늦게 실패하는 SDK 백그라운드 Task 예외가 프로세스를 오염시키지 않게 관찰한다.
            ObserveOrphanedFault(operationTask);
            callerToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{operationName} 작업이 전체 제한 시간을 초과했습니다.");
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            ObserveIfCreated(operationTask);
            throw;
        }
        catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested)
        {
            ObserveIfCreated(operationTask);
            throw new TimeoutException($"{operationName} 작업이 전체 제한 시간을 초과했습니다.", ex);
        }
        catch (ServiceResultException ex) when (callerToken.IsCancellationRequested && IsCancellationStatus(ex.StatusCode))
        {
            ObserveIfCreated(operationTask);
            throw new OperationCanceledException($"{operationName} 작업이 취소되었습니다.", ex, callerToken);
        }
        catch (ServiceResultException ex) when (timeoutSource.IsCancellationRequested && IsCancellationStatus(ex.StatusCode))
        {
            ObserveIfCreated(operationTask);
            throw new TimeoutException($"{operationName} 작업이 전체 제한 시간을 초과했습니다.", ex);
        }
    }

    private static bool IsCancellationStatus(uint statusCode) =>
        statusCode == StatusCodes.BadRequestInterrupted
        || statusCode == StatusCodes.BadRequestCancelledByClient
        || statusCode == StatusCodes.BadTimeout;

    private static void ObserveIfCreated(Task? task)
    {
        if (task is not null)
        {
            ObserveOrphanedFault(task);
        }
    }
}
