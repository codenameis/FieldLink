using System.Net;
using System.Net.Sockets;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Tcp;
using UdpClient = FieldLink.Communication.Udp.UdpClient;

namespace FieldLink.Communication.Tests;

internal static class ConnectionTests
{
    internal static async Task DisposalSurvivesCancellationCallbackFailureAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var entered = new TaskCompletionSource<ITcpTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> active = pair.Client.ExecuteTransactionAsync(transaction =>
        {
            entered.SetResult(transaction);
            return release.Task;
        });
        ITcpTransaction transaction = await entered.Task;
        var callbackError = new InvalidOperationException("Driver cancellation callback failed.");
        using var registration = transaction.CancellationToken.Register(() => throw callbackError);
        try
        {
            AggregateException error = await TestAssert.ThrowsAsync<AggregateException>(() =>
            {
                pair.Client.Dispose();
                return Task.CompletedTask;
            });
            TestAssert.True(error.Flatten().InnerExceptions.Contains(callbackError));
            await TestAssert.FailureAsync(() => active.WaitAsync(TimeSpan.FromSeconds(1)), CommunicationFailure.Disposed);
            TestAssert.Equal(ClientState.Disposed, pair.Client.State);
            await TestAssert.ThrowsAsync<ObjectDisposedException>(() => pair.Client.OpenAsync());
            try { TestAssert.Equal(0, await pair.Peer.ReceiveAsync(new byte[1], SocketFlags.None)); }
            catch (SocketException errorOnClose) when (errorOnClose.SocketErrorCode == SocketError.ConnectionReset) { }
        }
        finally { release.TrySetResult(true); }
    }

    internal static async Task TcpCloseCancelsQueuedOpenAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        long generation = pair.Client.ConnectionGeneration;
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<byte[]> active = pair.Client.ExchangeAsync([1], TcpFixture.Timeout, _ =>
        {
            entered.SetResult(true);
            if (!release.Wait(TcpFixture.Timeout))
                throw new TimeoutException("Test classifier was not released.");
            return ResponseDisposition.Accept;
        });
        await TcpFixture.ReadExactlyAsync(pair.Peer, 1);
        await TcpFixture.WriteAsync(pair.Peer, [2]);
        await entered.Task.WaitAsync(TcpFixture.Timeout);
        Task queuedOpen;
        try
        {
            // 중복 Open은 즉시 거부한다. 정상적인 재접속이 이전 요청의 정리를 기다리는 경우를 만든다.
            pair.Client.Close();
            queuedOpen = pair.Client.OpenAsync(TcpFixture.Timeout);
            TestAssert.True(!queuedOpen.IsCompleted, "Open must wait for the closed request to release its gate");
            pair.Client.Close();
        }
        finally { release.Set(); }
        await TestAssert.FailureAsync(() => active, CommunicationFailure.ConnectionClosed);
        await TestAssert.FailureAsync(() => queuedOpen, CommunicationFailure.ConnectionClosed);
        TestAssert.Equal(ClientState.Closed, pair.Client.State);
        TestAssert.Equal(generation, pair.Client.ConnectionGeneration);
        TestAssert.True(!pair.Listener.Pending(), "Close must prevent an earlier queued Open from reconnecting");

        await pair.Client.OpenAsync(TcpFixture.Timeout);
        using Socket peer = await pair.Listener.AcceptSocketAsync();
        await pair.Client.SendAsync([7]);
        TestAssert.Bytes([7], await TcpFixture.ReadExactlyAsync(peer, 1));
    }

    internal static async Task UdpCloseCancelsQueuedOpenAsync()
    {
        using var peer = new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using IUdpClient client = new UdpClient((IPEndPoint)peer.Client.LocalEndPoint!);
        await client.OpenAsync();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<byte[]> active = client.ExchangeAsync([1], TcpFixture.Timeout, _ =>
        {
            entered.SetResult(true);
            if (!release.Wait(TcpFixture.Timeout))
                throw new TimeoutException("Test classifier was not released.");
            return ResponseDisposition.Accept;
        });
        var received = await peer.ReceiveAsync();
        await peer.SendAsync(new byte[] { 2 }, received.RemoteEndPoint);
        await entered.Task.WaitAsync(TcpFixture.Timeout);
        Task queuedOpen;
        try
        {
            client.Close();
            queuedOpen = client.OpenAsync(TcpFixture.Timeout);
            TestAssert.True(!queuedOpen.IsCompleted);
            client.Close();
        }
        finally { release.Set(); }
        await TestAssert.ThrowsAsync<CommunicationException>(() => active);
        await TestAssert.FailureAsync(() => queuedOpen, CommunicationFailure.ConnectionClosed);
        TestAssert.Equal(ClientState.Closed, client.State);

        await client.OpenAsync();
        await client.SendAsync([2]);
        TestAssert.Bytes([2], (await peer.ReceiveAsync()).Buffer);
    }

    internal static async Task TcpCloseReleasesWaitingCallbackAsync()
    {
        using var pair = await TcpFixture.CreateAsync();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<ITcpTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> active = pair.Client.ExecuteTransactionAsync(transaction =>
        {
            entered.SetResult(transaction);
            return release.Task;
        }, TcpFixture.Timeout);
        ITcpTransaction endedTransaction = await entered.Task;
        pair.Client.Close();
        try
        {
            await TestAssert.FailureAsync(() => active.WaitAsync(TimeSpan.FromSeconds(1)),
                CommunicationFailure.ConnectionClosed);
            TestAssert.True(!release.Task.IsCompleted, "Client cleanup must not wait for caller code to finish");
            await pair.Client.OpenAsync(TimeSpan.FromSeconds(1));
            using Socket peer = await pair.Listener.AcceptSocketAsync();
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => endedTransaction.SendAsync([8]));
            await pair.Client.SendAsync([9]);
            TestAssert.Bytes([9], await TcpFixture.ReadExactlyAsync(peer, 1));
        }
        finally
        {
            release.TrySetResult(true);
            try { await active; } catch (CommunicationException) { }
        }
    }
}
