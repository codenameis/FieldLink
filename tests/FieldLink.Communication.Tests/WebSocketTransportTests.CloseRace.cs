using System.IO;
using System.Net;
using System.Net.WebSockets;
using FieldLink.Communication.WebSockets;

namespace FieldLink.Communication.Tests;

internal static partial class WebSocketTransportTests
{
    internal static async Task QueuedReceiveAndLateDataCloseOverLoopbackAsync()
    {
        int port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/close-race/");
        listener.Start();
        var allowData = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowReply = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            await allowData.Task;
            // 상대가 아직 Close를 읽기 전에 보낸 일반 메시지가 늦게 도착한다.
            await socket.SendAsync(new ArraySegment<byte>(new byte[] { (byte)'x' }),
                WebSocketMessageType.Text, true, CancellationToken.None);
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[32]), CancellationToken.None);
            TestAssert.Equal(WebSocketMessageType.Close, result.MessageType);
            closeSeen.TrySetResult(true);
            await allowReply.Task;
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        });
        try
        {
            using var connection = await WebSocketClient.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/close-race/"));
            Task<WebSocketMessage> active = connection.ReceiveAsync();
            Task<WebSocketMessage> queued = connection.ReceiveAsync();
            Task closing = connection.CloseAsync();
            TestAssert.True(SpinWait.SpinUntil(() => !connection.IsOpen, TimeSpan.FromSeconds(1)));
            allowData.TrySetResult(true);
            TestAssert.Equal("x", (await active.WaitAsync(TimeSpan.FromSeconds(1))).Text);
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => queued.WaitAsync(TimeSpan.FromSeconds(1)));
            await closeSeen.Task.WaitAsync(TimeSpan.FromSeconds(1));
            TestAssert.True(!closing.IsCompleted, "상대의 Close 응답 전에 종료했습니다.");
            allowReply.TrySetResult(true);
            await closing.WaitAsync(TimeSpan.FromSeconds(2));
            await peer.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            allowData.TrySetResult(true);
            allowReply.TrySetResult(true);
            listener.Close();
            try { await peer.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (Exception) { /* 실패한 검사 뒤에도 상대 작업을 정리한다. */ }
        }
    }

    internal static async Task QueuedReceiveCannotAbortCloseAfterLateDataAsync()
    {
        using var socket = new ClosingRaceSocket(deliverDataFirst: true);
        using var connection = new WebSocketConnection(socket, "test", 1024, TimeSpan.FromSeconds(2));
        Task<WebSocketMessage> active = connection.ReceiveAsync();
        await socket.FirstReceiveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<WebSocketMessage> queued = connection.ReceiveAsync();
        socket.AllowCloseOutput.TrySetResult(true);
        Task closing = connection.CloseAsync();
        await socket.CloseOutputEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.AllowData.TrySetResult(true);
        TestAssert.Equal("x", (await active.WaitAsync(TimeSpan.FromSeconds(2))).Text);
        var error = await TestAssert.ThrowsAsync<InvalidOperationException>(() => queued.WaitAsync(TimeSpan.FromSeconds(2)));
        TestAssert.Equal(typeof(InvalidOperationException), error.GetType());
        TestAssert.Equal(0, socket.PrematureAbortCount);
        TestAssert.True(!closing.IsCompleted, "Close 응답 전에 종료가 완료되었습니다.");
        await socket.CloseReceiveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.ReplyClose();
        await closing.WaitAsync(TimeSpan.FromSeconds(2));
        TestAssert.Equal(2, socket.ReceiveCount);
        TestAssert.Equal(0, socket.PrematureAbortCount);
    }

    internal static async Task QueuedSendCannotAbortCloseAsync()
    {
        using var socket = new ClosingRaceSocket(deliverDataFirst: false);
        using var connection = new WebSocketConnection(socket, "test", 1024, TimeSpan.FromSeconds(2));
        Task active = connection.SendTextAsync("first");
        await socket.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task closing = connection.CloseAsync();
        Task queued = connection.SendTextAsync("queued");
        socket.AllowSend.TrySetResult(true);
        await active.WaitAsync(TimeSpan.FromSeconds(2));
        await socket.CloseOutputEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.AllowCloseOutput.TrySetResult(true);
        var error = await TestAssert.ThrowsAsync<InvalidOperationException>(() => queued.WaitAsync(TimeSpan.FromSeconds(2)));
        TestAssert.Equal(typeof(InvalidOperationException), error.GetType());
        TestAssert.Equal(0, socket.PrematureAbortCount);
        TestAssert.Equal(1, socket.SendCount);
        TestAssert.True(!closing.IsCompleted);
        await socket.CloseReceiveEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        socket.ReplyClose();
        await closing.WaitAsync(TimeSpan.FromSeconds(2));
        TestAssert.Equal(0, socket.PrematureAbortCount);
    }

    internal static async Task ActiveWebSocketFailuresStillAbortAsync()
    {
        foreach (bool sending in new[] { true, false })
        {
            using var socket = new ClosingRaceSocket(deliverDataFirst: true);
            using var connection = new WebSocketConnection(socket, "test", 1024, TimeSpan.FromSeconds(2));
            var cause = new IOException("송수신 실패 재현");
            Task operation = sending ? connection.SendTextAsync("x") : connection.ReceiveAsync();
            if (sending)
                socket.AllowSend.TrySetException(cause);
            else
                socket.AllowData.TrySetException(cause);
            var actual = await TestAssert.ThrowsAsync<IOException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2)));
            TestAssert.True(ReferenceEquals(cause, actual));
            TestAssert.Equal(1, socket.PrematureAbortCount);
            TestAssert.True(!connection.IsOpen);
        }
    }

    // 실제 잠금과 종료 코드를 사용하면서 상대의 데이터·Close 도착 순서만 제어한다.
    private sealed class ClosingRaceSocket(bool deliverDataFirst) : System.Net.WebSockets.WebSocket
    {
        private int state = (int)WebSocketState.Open;
        private int receiving;
        internal int ReceiveCount;
        internal int SendCount;
        internal int PrematureAbortCount;
        internal readonly TaskCompletionSource<bool> FirstReceiveEntered = Signal();
        internal readonly TaskCompletionSource<bool> CloseReceiveEntered = Signal();
        internal readonly TaskCompletionSource<bool> CloseOutputEntered = Signal();
        internal readonly TaskCompletionSource<bool> SendEntered = Signal();
        internal readonly TaskCompletionSource<bool> AllowData = Signal();
        internal readonly TaskCompletionSource<bool> AllowSend = Signal();
        internal readonly TaskCompletionSource<bool> AllowCloseOutput = Signal();
        private readonly TaskCompletionSource<bool> closeReply = Signal();
        private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override WebSocketState State => (WebSocketState)Volatile.Read(ref state);
        public override WebSocketCloseStatus? CloseStatus => State == WebSocketState.Closed ? WebSocketCloseStatus.NormalClosure : null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref SendCount);
            SendEntered.TrySetResult(true);
            await AllowSend.Task;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            TestAssert.Equal(1, Interlocked.Increment(ref receiving));
            int count = Interlocked.Increment(ref ReceiveCount);
            FirstReceiveEntered.TrySetResult(true);
            try
            {
                if (deliverDataFirst && count == 1)
                {
                    await AllowData.Task;
                    buffer.Array![buffer.Offset] = (byte)'x';
                    return new WebSocketReceiveResult(1, WebSocketMessageType.Text, true);
                }
                CloseReceiveEntered.TrySetResult(true);
                await closeReply.Task;
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "");
            }
            finally { Interlocked.Decrement(ref receiving); }
        }

        public override async Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
            Volatile.Write(ref state, (int)WebSocketState.CloseSent);
            CloseOutputEntered.TrySetResult(true);
            await AllowCloseOutput.Task;
        }

        internal void ReplyClose()
        {
            Volatile.Write(ref state, (int)WebSocketState.Closed);
            closeReply.TrySetResult(true);
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public override void Abort()
        {
            if (State != WebSocketState.Closed && State != WebSocketState.Aborted)
            {
                Interlocked.Increment(ref PrematureAbortCount);
                Volatile.Write(ref state, (int)WebSocketState.Aborted);
            }
            AllowData.TrySetCanceled();
            AllowSend.TrySetCanceled();
            AllowCloseOutput.TrySetCanceled();
            closeReply.TrySetCanceled();
        }

        public override void Dispose() => Abort();
    }
}
