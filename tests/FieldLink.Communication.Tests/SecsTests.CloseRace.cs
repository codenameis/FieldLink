using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Tcp;
using FieldLink.Secs;
using FieldLink.Secs.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class SecsTests
{
    internal static async Task GracefulCloseEofBeforeEndWriteDoesNotAbortSendAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var socket = new DelayedCompletionTcpClient();
            Task<System.Net.Sockets.TcpClient> accept = listener.AcceptTcpClientAsync();
            await socket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using var peer = await accept;
            using var session = new HsmsSession(socket, 1, TimeSpan.FromSeconds(5), 1024, Encoding.ASCII, false);
            using var stream = new DelayedEndWriteStream(socket.Client);
            socket.DelayedStream = stream;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var connection = (TcpDuplexConnection)typeof(HsmsSession).GetField("connection", fields)!.GetValue(session)!;
            typeof(TcpDuplexConnection).GetField("stream", fields)!.SetValue(connection, stream);
            session.Start();
            Task closing = session.CloseAsync();
            try
            {
                var separate = HsmsCodec.Decode(await ReadRawFrameAsync(peer.GetStream()));
                TestAssert.Equal((byte)9, separate.SessionType);
                TestAssert.Equal((ushort)65535, separate.DeviceID);
                await stream.WriteReached.Task.WaitAsync(TimeSpan.FromSeconds(2));
                // The real socket has delivered Separate, but EndWrite has not run yet.
                peer.Client.Shutdown(SocketShutdown.Send);
                var wait = Stopwatch.StartNew();
                while (session.LastError == null && wait.Elapsed < TimeSpan.FromSeconds(2))
                    await Task.Delay(1);
                TestAssert.True(session.LastError is CommunicationException, "The receive loop must observe peer EOF");
                bool completedBeforeSend = session.Completion.IsCompleted;
                stream.AllowEndWrite.TrySetResult(true);
                await closing.WaitAsync(TimeSpan.FromSeconds(2));
                await session.Completion.WaitAsync(TimeSpan.FromSeconds(2));
                TestAssert.True(!completedBeforeSend, "Completion must retain the connection until close send finishes");
                TestAssert.True(stream.IsDisposed && !session.IsOpen);
            }
            finally
            {
                stream.AllowEndWrite.TrySetResult(true);
                session.Dispose();
                // Observe the send even if an earlier assertion failed; never leave a gated operation behind.
                await closing.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    internal static Task GracefulCloseDisposeStillAbortsPendingEndWriteAsync() =>
        WithDelayedCloseAsync(TimeSpan.FromSeconds(5), CancellationToken.None, async (session, stream, closing) =>
        {
            session.Dispose();
            TestAssert.True(stream.IsDisposed, "Dispose must release immediately, even during graceful close");
            stream.AllowEndWrite.TrySetResult(true);
            var error = await TestAssert.FailureAsync(() => closing, CommunicationFailure.ConnectionClosed);
            TestAssert.Equal(CommunicationStage.Sending, error.Stage);
            TestAssert.True(error.InnerException is ObjectDisposedException);
            await session.Completion;
        });

    internal static async Task GracefulCloseCancellationStillAbortsPendingEndWriteAsync()
    {
        using var cancel = new CancellationTokenSource();
        await WithDelayedCloseAsync(TimeSpan.FromSeconds(5), cancel.Token, async (session, stream, closing) =>
        {
            cancel.Cancel();
            TestAssert.True(stream.IsDisposed, "Caller cancellation must release the active send");
            stream.AllowEndWrite.TrySetResult(true);
            await TestAssert.ThrowsAsync<OperationCanceledException>(() => closing);
            await session.Completion;
            TestAssert.True(!session.IsOpen);
        });
    }

    internal static Task GracefulCloseDeadlineStillAbortsPendingEndWriteAsync() =>
        WithDelayedCloseAsync(TimeSpan.FromSeconds(1), CancellationToken.None, async (session, stream, closing) =>
        {
            await stream.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            stream.AllowEndWrite.TrySetResult(true);
            await TestAssert.ThrowsAsync<TimeoutException>(() => closing);
            await session.Completion;
            TestAssert.True(!session.IsOpen);
        });

    internal static Task GracefulCloseSendFailureIsNotConvertedToSuccessAsync() =>
        WithDelayedCloseAsync(TimeSpan.FromSeconds(5), CancellationToken.None, async (session, stream, closing) =>
        {
            var cause = new IOException("send failed", new SocketException((int)SocketError.ConnectionReset));
            stream.CompletionError = cause;
            stream.AllowEndWrite.TrySetResult(true);
            var error = await TestAssert.FailureAsync(() => closing, CommunicationFailure.SocketError);
            TestAssert.Equal(CommunicationStage.Sending, error.Stage);
            TestAssert.True(ReferenceEquals(cause, error.InnerException));
            await session.Completion;
            TestAssert.True(stream.IsDisposed && !session.IsOpen);
        });

    private static async Task WithDelayedCloseAsync(TimeSpan timeout, CancellationToken cancellationToken,
        Func<HsmsSession, DelayedEndWriteStream, Task, Task> verify)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var socket = new DelayedCompletionTcpClient();
            Task<System.Net.Sockets.TcpClient> accept = listener.AcceptTcpClientAsync();
            await socket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using var peer = await accept;
            using var session = new HsmsSession(socket, 1, timeout, 1024, Encoding.ASCII, false);
            using var stream = new DelayedEndWriteStream(socket.Client);
            socket.DelayedStream = stream;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var connection = (TcpDuplexConnection)typeof(HsmsSession).GetField("connection", fields)!.GetValue(session)!;
            typeof(TcpDuplexConnection).GetField("stream", fields)!.SetValue(connection, stream);
            session.Start();
            Task closing = session.CloseAsync(cancellationToken);
            try
            {
                TestAssert.Equal((byte)9, HsmsCodec.Decode(await ReadRawFrameAsync(peer.GetStream())).SessionType);
                await stream.WriteReached.Task.WaitAsync(TimeSpan.FromSeconds(2));
                await verify(session, stream, closing);
            }
            finally
            {
                stream.AllowEndWrite.TrySetResult(true);
                session.Dispose();
                await closing.ContinueWith(task => { _ = task.Exception; }, TaskScheduler.Default);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class DelayedCompletionTcpClient : System.Net.Sockets.TcpClient
    {
        internal NetworkStream? DelayedStream;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DelayedStream?.Dispose();
            base.Dispose(disposing);
        }
    }

    // Keep real BeginWrite/EndWrite behavior while controlling only completion ordering.
    private sealed class DelayedEndWriteStream(Socket socket) : NetworkStream(socket, false)
    {
        internal readonly TaskCompletionSource<bool> WriteReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> AllowEndWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? CompletionError;
        internal bool IsDisposed { get; private set; }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            base.BeginWrite(buffer, offset, count, result =>
            {
                WriteReached.TrySetResult(true);
                _ = FinishWriteAsync(result, completion);
            }, null);
            return completion.Task;
        }

        private async Task FinishWriteAsync(IAsyncResult result, TaskCompletionSource<bool> completion)
        {
            await AllowEndWrite.Task.ConfigureAwait(false);
            try
            {
                base.EndWrite(result);
                if (CompletionError != null)
                    throw CompletionError;
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                IsDisposed = true;
            base.Dispose(disposing);
            if (disposing)
                Disposed.TrySetResult(true);
        }
    }
}
