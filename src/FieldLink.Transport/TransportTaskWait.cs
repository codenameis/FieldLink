using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport;

internal static class TransportTaskWait
{
    public static void ObserveFaultSafely(Task? task)
    {
        if (task is null)
        {
            return;
        }

        if (task.IsCompleted)
        {
            if (task.IsFaulted)
            {
                _ = task.Exception;
            }

            return;
        }

        _ = task.ContinueWith(
            static faultedTask => _ = faultedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public static async Task<TResult> WaitWithCancellationAsync<TResult>(
        Task<TResult> targetTask,
        CancellationToken cancellationToken)
    {
        if (!targetTask.IsCompleted && cancellationToken.CanBeCanceled)
        {
            var cancellationSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                static state =>
                {
                    if (state is TaskCompletionSource<bool> signal)
                    {
                        signal.TrySetResult(true);
                    }
                },
                cancellationSignal))
            {
                Task completedTask = await Task.WhenAny(targetTask, cancellationSignal.Task).ConfigureAwait(false);
                if (completedTask != targetTask)
                {
                    ObserveFaultSafely(targetTask);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        return await targetTask.ConfigureAwait(false);
    }

    public static Task CreateCanceledTask(CancellationToken cancellationToken)
    {
        var canceledCompletion = new TaskCompletionSource<bool>();
        if (cancellationToken.CanBeCanceled)
        {
            canceledCompletion.TrySetCanceled(cancellationToken);
        }
        else
        {
            canceledCompletion.TrySetCanceled();
        }

        return canceledCompletion.Task;
    }
}
