using System.Net;
using System.Net.Sockets;
using Server = FieldLink.Communication.Tcp.TcpServer;

namespace FieldLink.Communication.Tests;

internal static partial class RobotClientTests
{
    internal static Task TcpServerStopDoesNotHoldLockDuringCancellationAsync() => AssertServerCancellationLockAsync(false);
    internal static Task TcpServerDisposeDoesNotHoldLockDuringCancellationAsync() => AssertServerCancellationLockAsync(true);
    internal static Task TcpServerAcceptFailureDoesNotHoldLockDuringCancellationAsync() => AssertServerCancellationLockAsync(false, true);

    private static async Task AssertServerCancellationLockAsync(bool dispose, bool failAccept = false)
    {
        object applicationLock = new();
        using var applicationLockHeld = new ManualResetEventSlim();
        using var callbackEntered = new ManualResetEventSlim();
        var registered = NewServerSignal();
        var releaseWorker = NewServerSignal();
        bool callbackAcquiredLock = false;
        using var server = new Server(new IPEndPoint(IPAddress.Loopback, 0), async (_, token) =>
        {
            using var registration = token.Register(() =>
            {
                callbackEntered.Set();
                // 이전 잠금 역전을 무한 교착 없이 드러내기 위한 안전 종료 시간이다.
                callbackAcquiredLock = Monitor.TryEnter(applicationLock, TimeSpan.FromSeconds(2));
                if (callbackAcquiredLock)
                    Monitor.Exit(applicationLock);
            });
            registered.TrySetResult(true);
            await releaseWorker.Task;
        });
        await server.StartAsync();
        using var peer = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await peer.ConnectAsync(server.LocalEndPoint);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task observer = Task.Factory.StartNew(() =>
        {
            lock (applicationLock)
            {
                applicationLockHeld.Set();
                TestAssert.True(callbackEntered.Wait(TimeSpan.FromSeconds(3)), "취소 콜백이 시작되어야 합니다.");
                TestAssert.True(!server.IsRunning);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        TestAssert.True(applicationLockHeld.Wait(TimeSpan.FromSeconds(3)));
        Task stopping = Task.Run(async () =>
        {
            if (failAccept)
            {
                // 외부 장비 없이 Accept 오류의 finally 경로를 강제로 실행한다.
                object run = typeof(Server).GetField("current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(server)!;
                var listener = (TcpListener)run.GetType().GetField("Listener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(run)!;
                listener.Stop();
                await TestAssert.ThrowsAsync<Exception>(() => server.Completion);
            }
            else if (dispose)
                server.Dispose();
            else
                await server.StopAsync();
        });
        try
        {
            await observer.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            releaseWorker.TrySetResult(true);
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            if (failAccept)
                await TestAssert.ThrowsAsync<Exception>(() => server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            else
                await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        TestAssert.True(callbackAcquiredLock, "서버 잠금과 앱 잠금의 순서가 역전되었습니다.");
    }

    internal static Task TcpServerConcurrentStopsWaitForCallbacksAndWorkersBeforeRestartAsync() => AssertServerStopCompletionAsync(false);
    internal static Task TcpServerConcurrentStopAndDisposeCleanUpOnceAsync() => AssertServerStopCompletionAsync(true);

    private static async Task AssertServerStopCompletionAsync(bool disposeDuringStop)
    {
        var registered = NewServerSignal();
        var callbackEntered = NewServerSignal();
        using var releaseCallback = new ManualResetEventSlim();
        var releaseWorker = NewServerSignal();
        int callbacks = 0, handlers = 0;
        using var server = new Server(new IPEndPoint(IPAddress.Loopback, 0), async (_, token) =>
        {
            Interlocked.Increment(ref handlers);
            using var registration = token.Register(() =>
            {
                Interlocked.Increment(ref callbacks);
                callbackEntered.TrySetResult(true);
                TestAssert.True(releaseCallback.Wait(TimeSpan.FromSeconds(5)));
            });
            registered.TrySetResult(true);
            await releaseWorker.Task;
        });
        await server.StartAsync();
        IPEndPoint endpoint = server.LocalEndPoint;
        using var peer = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await peer.ConnectAsync(endpoint);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task firstStop = Task.Run(() => server.StopAsync());
        Task secondStop = Task.CompletedTask;
        try
        {
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            TestAssert.True(!server.IsRunning);
            TestAssert.Equal(endpoint, server.LocalEndPoint);
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync());
            secondStop = server.StopAsync();
            TestAssert.True(!firstStop.IsCompleted && !secondStop.IsCompleted && !server.Completion.IsCompleted);
            using var rejectedPeer = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await TestAssert.ThrowsAsync<SocketException>(() => rejectedPeer.ConnectAsync(endpoint));
            if (disposeDuringStop)
            {
                server.Dispose();
                server.Dispose();
            }
            releaseCallback.Set();
            TestAssert.True(!firstStop.IsCompleted && !secondStop.IsCompleted, "연결 작업자 완료 전 Stop이 끝나면 안 됩니다.");
        }
        finally
        {
            releaseCallback.Set();
            releaseWorker.TrySetResult(true);
            await Task.WhenAll(firstStop, secondStop).WaitAsync(TimeSpan.FromSeconds(5));
        }
        TestAssert.Equal(1, callbacks);
        TestAssert.Equal(1, handlers);
        TestAssert.True(server.LastConnectionError == null);
        if (disposeDuringStop)
            await TestAssert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync());
        else
        {
            await server.StartAsync();
            TestAssert.True(server.IsRunning);
            await server.StopAsync();
        }
    }

    private static TaskCompletionSource<bool> NewServerSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
