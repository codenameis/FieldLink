using System.Net;
using System.Net.Sockets;
using System.Text;
using FieldLink.Secs;
using FieldLink.Secs.Protocols;
using FieldLink.Secs.Types;

namespace FieldLink.Communication.Tests;

internal static partial class SecsTests
{
    internal static async Task GemCommandsPreserveAcknowledgementsAndRejectMalformedResponsesAsync()
    {
        var fake = new GemPeer();
        var gem = new Gem(fake, Encoding.ASCII);
        fake.Reply = new SecsValue(new object[] { (SecsValue)new VariableName { ID = 42, Name = "SV", Units = "C" } });
        var names = await gem.StatusVariableNamelistAsync(new[] { 42 });
        TestAssert.Equal(42L, names[0].ID);
        TestAssert.Equal("SV", names[0].Name);
        TestAssert.Bytes(TestBytes.FromHexString("71040000002A"), fake.LastBody);
        TestAssert.Equal((byte)11, fake.Function);
        fake.Reply = new SecsValue(new byte[] { 2 });
        TestAssert.Equal((byte)2, await gem.OnlineRequestAsync());
        TestAssert.Equal((byte)17, fake.Function);
        TestAssert.Equal((byte)2, await gem.OfflineRequestAsync());
        TestAssert.Equal((byte)15, fake.Function);
        fake.Reply = new SecsValue(2);
        await TestAssert.ThrowsAsync<InvalidDataException>(async () => await gem.OnlineRequestAsync());
        fake.Reply = new SecsValue(new object[] { new byte[] { 1 }, SecsValue.EmptyListValue() });
        var denial = await TestAssert.ThrowsAsync<SecsProtocolException>(async () => await gem.EstablishCommunicationsAsync());
        TestAssert.Equal(1, denial.Code);
        fake.Reply = SecsValue.EmptyListValue();
        await TestAssert.ThrowsAsync<InvalidDataException>(async () => await gem.EstablishCommunicationsAsync());
        await gem.EquipmentConstantRequestAsync();
        TestAssert.Equal((byte)2, fake.Stream);
        TestAssert.Equal((byte)13, fake.Function);
        TestAssert.Bytes(new byte[] { 1, 0 }, fake.LastBody);
        fake.WrongHeader = true;
        await TestAssert.ThrowsAsync<InvalidDataException>(async () => await gem.EquipmentConstantRequestAsync());
    }

    internal static async Task SelectRejectionAndOpenCancellationLeaveNoReadySessionAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new HsmsClient((IPEndPoint)listener.LocalEndpoint, selectOnOpen: true);
            var open = client.OpenAsync();
            using (var raw = await listener.AcceptTcpClientAsync())
            {
                var select = HsmsCodec.Decode(await ReadRawFrameAsync(raw.GetStream()));
                TestAssert.Equal((byte)1, select.SessionType);
                byte[] denied = HsmsCodec.Encode(new SecsMessage(65535, 0, 2, select.MessageID, sessionType: 2));
                await raw.GetStream().WriteAsync(denied, 0, denied.Length);
                var error = await TestAssert.ThrowsAsync<SecsProtocolException>(async () => await open);
                TestAssert.Equal(2, error.Code);
                TestAssert.True(!client.IsOpen);
            }
            using var cancel = new CancellationTokenSource();
            var opening = client.OpenAsync(cancel.Token);
            using var peer = await listener.AcceptTcpClientAsync();
            await ReadRawFrameAsync(peer.GetStream());
            await TestAssert.ThrowsAsync<InvalidOperationException>(() => client.RequestAsync(1, 1, new SecsValue()));
            cancel.Cancel();
            await TestAssert.ThrowsAsync<OperationCanceledException>(async () => await opening);
            TestAssert.True(!client.IsOpen);
        }
        finally { listener.Stop(); }
    }

    internal static async Task OutboundLimitsAndInvalidCallsDoNotBreakHealthySessionAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0), automaticGemReplies: true);
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint, maximumFrameLength: 32);
        await client.OpenAsync();
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.RequestAsync(128, 1, new SecsValue()));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.RequestAsync(1, 2, new SecsValue()));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.RequestAsync(1, 1, new SecsValue(new byte[20])));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => client.RequestAsync(1, 1, new SecsValue(), cancelled.Token));
        await TestAssert.ThrowsAsync<ArgumentException>(() => client.Session.ReplyAsync(new SecsMessage(1, 1, 1, 1), new SecsValue()));
        await client.Session.LinkTestAsync();
        TestAssert.Equal("", (await client.Gem.AreYouThereAsync()).ModelType);
        await client.CloseAsync();
        await server.StopAsync();
    }

    internal static async Task PeerAbortAndDisconnectFailTheCorrectPendingRequestsAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var client = new HsmsClient((IPEndPoint)listener.LocalEndpoint, automaticGemReplies: false);
            var accept = listener.AcceptTcpClientAsync();
            await client.OpenAsync();
            using var raw = await accept;
            var request = client.RequestAsync(3, 1, new SecsValue());
            var primary = HsmsCodec.Decode(await ReadRawFrameAsync(raw.GetStream()));
            var abort = HsmsCodec.Encode(new SecsMessage(primary.DeviceID, primary.StreamNo, 0, primary.MessageID));
            await raw.GetStream().WriteAsync(abort, 0, abort.Length);
            await TestAssert.ThrowsAsync<SecsProtocolException>(async () => await request);
            TestAssert.True(client.IsOpen);
            Task<SecsMessage>[] pending = Enumerable.Range(0, 4).Select(_ => client.RequestAsync(3, 1, new SecsValue())).ToArray();
            for (int i = 0; i < 4; i++)
                await ReadRawFrameAsync(raw.GetStream());
            raw.Dispose();
            foreach (var task in pending)
                await TestAssert.ThrowsAsync<IOException>(async () => await task);
            TestAssert.True(!client.IsOpen);
        }
        finally { listener.Stop(); }
    }

    internal static async Task HandlerFailuresAreObservableAndDoNotStopReceptionAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        var connected = new TaskCompletionSource<HsmsSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (session, _) => connected.TrySetResult(session);
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint);
        client.MessageReceived += (_, __) => throw new InvalidOperationException("callback failure");
        await client.OpenAsync();
        await client.Session.SendAsync(6, 11, new SecsValue());
        await connected.Task;
        await server.PublishAsync(6, 11, new SecsValue("event"));
        var session = client.Session;
        for (int i = 0; i < 100 && session.LastMessageHandlerError == null; i++)
            await Task.Delay(5);
        TestAssert.True(session.LastMessageHandlerError is InvalidOperationException);
        await session.LinkTestAsync();
        await client.CloseAsync();
        await server.StopAsync();
    }

    internal static async Task ConcurrentStopAndDisposeCompleteActiveSessionsAsync()
    {
        var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint);
        await client.OpenAsync();
        var session = client.Session;
        await session.LinkTestAsync();
        var first = Task.Run(() => server.StopAsync());
        var second = Task.Run(() => server.StopAsync());
        var dispose = Task.Run(server.Dispose);
        await Task.WhenAll(first, second, dispose);
        await session.Completion;
        TestAssert.True(!server.IsRunning && !client.IsOpen);
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync());
    }

    internal static async Task GracefulCloseImmediatelyRejectsNewRequestsAndIsSharedAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint);
        await client.OpenAsync();
        var session = client.Session;
        Task close = session.CloseAsync();
        TestAssert.True(!session.IsOpen);
        TestAssert.True(ReferenceEquals(close, session.CloseAsync()));
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => session.RequestAsync(1, 1, new SecsValue()));
        await close;
        await session.Completion;
        await server.StopAsync();
    }

    internal static async Task CancellingGracefulCloseStillReleasesConnectionAsync()
    {
        using var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0));
        await server.StartAsync();
        using var client = new HsmsClient(server.LocalEndPoint);
        await client.OpenAsync();
        var session = client.Session;
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await TestAssert.ThrowsAsync<OperationCanceledException>(() => session.CloseAsync(cancel.Token));
        await session.Completion;
        TestAssert.True(!client.IsOpen);
        await server.StopAsync();
    }

    private sealed class GemPeer : ISecs
    {
        internal SecsValue Reply = new();
        internal byte Stream, Function;
        internal byte[] LastBody = Array.Empty<byte>();
        internal bool WrongHeader;
        public Task<SecsMessage> RequestAsync(byte stream, byte function, SecsValue data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream = stream;
            Function = function;
            LastBody = data.ToSourceBytes(Encoding.ASCII);
            return Task.FromResult(new SecsMessage(1, stream, (byte)(function + (WrongHeader ? 3 : 1)), 7, Reply.ToSourceBytes(Encoding.ASCII)));
        }
    }
}
