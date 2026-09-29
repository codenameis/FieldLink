namespace FieldLink.Communication.Tests;

internal static class TestTasks
{
    // 이전 Task.WaitAsync와 같이 대기만 제한한다. 작업 자체의 취소는 호출자의 토큰/전송이 담당한다.
    internal static async Task WaitAsync(this Task task, TimeSpan timeout)
    {
        using (var stop = new CancellationTokenSource())
        {
            Task delay = Task.Delay(timeout, stop.Token);
            if (await Task.WhenAny(task, delay).ConfigureAwait(false) != task)
                throw new TimeoutException("테스트 작업의 대기 시간이 초과되었습니다.");
            stop.Cancel();
            await task.ConfigureAwait(false);
        }
    }

    internal static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
    {
        await ((Task)task).WaitAsync(timeout).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}
