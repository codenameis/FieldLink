using FieldLink.Communication.FileTransfer;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Reflection;

namespace FieldLink.Communication.Tests;

internal static class FileTransferTests
{
    internal static async Task UploadDownloadAndAtomicReplaceAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldlink-file-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var address = new Uri($"http://127.0.0.1:{FreePort()}/files/");
            using var server = new FileTransferServer(address, Path.Combine(root, "server"),
                request => request.Headers["X-Process-Key"] == "secret", maximumFileBytes: 16);
            await server.StartAsync();
            string source = Path.Combine(root, "source.bin");
            string output = Path.Combine(root, "output.bin");
            File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
            using (var unauthenticated = new FileTransferClient(address))
                await TestAssert.ThrowsAsync<HttpRequestException>(() => unauthenticated.UploadAsync(source, "batch.bin"));
            using (var client = new FileTransferClient(address,
                request => request.Headers.Add("X-Process-Key", "secret"), maximumFileBytes: 16))
            {
                await client.UploadAsync(source, "batch.bin");
                await client.DownloadAsync("batch.bin", output);
                TestAssert.Bytes(new byte[] { 1, 2, 3 }, File.ReadAllBytes(output));
                File.WriteAllBytes(source, new byte[] { 4, 5 });
                await client.UploadAsync(source, "batch.bin");
                await client.DownloadAsync("batch.bin", output);
                TestAssert.Bytes(new byte[] { 4, 5 }, File.ReadAllBytes(output));
                using (var limited = new FileTransferClient(address,
                    request => request.Headers.Add("X-Process-Key", "secret"), maximumFileBytes: 1))
                    await TestAssert.ThrowsAsync<InvalidDataException>(() => limited.DownloadAsync("batch.bin", output));
                TestAssert.Bytes(new byte[] { 4, 5 }, File.ReadAllBytes(output));
                await TestAssert.ThrowsAsync<ArgumentException>(() => client.UploadAsync(source, "../escape"));
                await TestAssert.ThrowsAsync<ArgumentException>(() => client.DownloadAsync("CON.txt", output));
                await TestAssert.ThrowsAsync<HttpRequestException>(() => client.DownloadAsync("missing.bin", output));
            }
            TestAssert.True(!File.Exists(Path.Combine(root, "escape")));
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { Directory.Delete(root, true); }
    }

    internal static async Task OversizeUploadLeavesNoTargetAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldlink-file-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var address = new Uri($"http://127.0.0.1:{FreePort()}/files/");
            using var server = new FileTransferServer(address, Path.Combine(root, "server"), _ => true, maximumFileBytes: 4);
            await server.StartAsync();
            using var client = new HttpClient();
            using var response = await client.PutAsync(new Uri(address, "large.bin"),
                new ByteArrayContent(Encoding.UTF8.GetBytes("12345")));
            TestAssert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            TestAssert.True(!File.Exists(Path.Combine(root, "server", "large.bin")));
            using var chunked = new HttpRequestMessage(HttpMethod.Put, new Uri(address, "chunked.bin"));
            chunked.Headers.TransferEncodingChunked = true;
            chunked.Headers.TryAddWithoutValidation("X-Content-SHA256", new string('0', 64));
            chunked.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("12345"));
            using var chunkedResponse = await client.SendAsync(chunked);
            TestAssert.Equal(HttpStatusCode.RequestEntityTooLarge, chunkedResponse.StatusCode);
            TestAssert.True(!File.Exists(Path.Combine(root, "server", "chunked.bin")));
            using var wrongHash = new HttpRequestMessage(HttpMethod.Put, new Uri(address, "wrong.bin"));
            wrongHash.Headers.TryAddWithoutValidation("X-Content-SHA256", new string('0', 64));
            wrongHash.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("1234"));
            using var wrongHashResponse = await client.SendAsync(wrongHash);
            TestAssert.Equal(HttpStatusCode.BadRequest, wrongHashResponse.StatusCode);
            TestAssert.True(!File.Exists(Path.Combine(root, "server", "wrong.bin")));
            await server.StopAsync();
            TestAssert.True(Directory.GetFiles(Path.Combine(root, "server")).Length == 0);
        }
        finally { Directory.Delete(root, true); }
    }

    internal static async Task DownloadHashMismatchPreservesLocalFileAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldlink-file-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string target = Path.Combine(root, "existing.bin");
            File.WriteAllBytes(target, new byte[] { 7, 8 });
            var address = new Uri($"http://127.0.0.1:{FreePort()}/files/");
            using var listener = new HttpListener();
            listener.Prefixes.Add(address.AbsoluteUri);
            listener.Start();
            Task serve = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                context.Response.Headers["X-Content-SHA256"] = new string('0', 64);
                context.Response.ContentLength64 = 3;
                await context.Response.OutputStream.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3);
                context.Response.Close();
            });
            using var client = new FileTransferClient(address);
            await TestAssert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync("batch.bin", target));
            await serve;
            TestAssert.Bytes(new byte[] { 7, 8 }, File.ReadAllBytes(target));
            TestAssert.True(Directory.GetFiles(root).Length == 1);
        }
        finally { Directory.Delete(root, true); }
    }

    internal static Task StalledBodyHonorsTimeoutAsync() => StalledBodyStopsAsync(false);
    internal static Task StalledBodyHonorsCancellationAsync() => StalledBodyStopsAsync(true);

    private static async Task StalledBodyStopsAsync(bool cancelByCaller)
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldlink-file-stall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var address = new Uri($"http://127.0.0.1:{FreePort()}/files/");
        using var listener = new HttpListener();
        listener.Prefixes.Add(address.AbsoluteUri);
        listener.Start();
        var bodyStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task serve = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            try
            {
                context.Response.Headers["X-Content-SHA256"] = new string('0', 64);
                context.Response.ContentLength64 = 2;
                await context.Response.OutputStream.WriteAsync(new byte[] { 1 }, 0, 1);
                await context.Response.OutputStream.FlushAsync();
                bodyStarted.TrySetResult(true);
                await release.Task;
            }
            finally { try { context.Response.Abort(); } catch (Exception) { } }
        });
        try
        {
            string target = Path.Combine(root, "existing.bin");
            File.WriteAllBytes(target, new byte[] { 7 });
            using var client = new FileTransferClient(address, timeout: TimeSpan.FromMilliseconds(500));
            using var cancellation = new CancellationTokenSource();
            Task download = client.DownloadAsync("batch.bin", target, cancellation.Token);
            await bodyStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            if (cancelByCaller) cancellation.Cancel();
            Task completed = await Task.WhenAny(download, Task.Delay(TimeSpan.FromSeconds(3)));
            TestAssert.True(completed == download, "멈춘 응답 본문에서 다운로드가 끝나지 않았습니다.");
            await TestAssert.ThrowsAsync<OperationCanceledException>(() => download);
            TestAssert.Bytes(new byte[] { 7 }, File.ReadAllBytes(target));
            TestAssert.True(Directory.GetFiles(root).Length == 1);
        }
        finally
        {
            release.TrySetResult(true);
            listener.Close();
            try { await serve.WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
            Directory.Delete(root, true);
        }
    }

    internal static async Task StopWaitsForInProgressSignalAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "fieldlink-file-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var address = new Uri($"http://127.0.0.1:{FreePort()}/files/");
            using var server = new FileTransferServer(address, root, _ => true);
            await server.StartAsync();
            object run = typeof(FileTransferServer).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(server);
            var cancellation = (CancellationTokenSource)run.GetType().GetField("Cancellation",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(run);
            var listener = (HttpListener)run.GetType().GetField("Listener",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(run);
            var signaling = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellation.Token.Register(() =>
            {
                signaling.TrySetResult(true);
                release.Task.GetAwaiter().GetResult();
            });
            Task first = Task.Run(() => server.StopAsync());
            try
            {
                await signaling.Task.WaitAsync(TimeSpan.FromSeconds(2));
                listener.Stop();
                Task second = server.StopAsync();
                server.Dispose();
                TestAssert.True(await Task.WhenAny(server.Completion, Task.Delay(100)) != server.Completion);
                TestAssert.True(!second.IsCompleted);
                release.TrySetResult(true);
                await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
            }
            finally { release.TrySetResult(true); }
        }
        finally { Directory.Delete(root, true); }
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }
}
