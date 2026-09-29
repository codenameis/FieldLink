using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.FileTransfer
{
    /// <summary>인증된 HTTP(S) 요청으로 제한된 디렉터리의 단일 파일을 전송합니다.</summary>
    public sealed class FileTransferServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly string prefix;
        private readonly string path;
        private readonly string root;
        private readonly Func<HttpListenerRequest, bool> authorize;
        private readonly long maximumFileBytes;
        private readonly SemaphoreSlim writeGate = new SemaphoreSlim(1, 1);
        private Run current;
        private bool disposed;
        private Exception lastRequestError;

        /// <summary>주소는 /files/처럼 슬래시로 끝나야 하며, 저장 디렉터리는 서버가 독점 관리해야 합니다.</summary>
        public FileTransferServer(Uri address, string rootDirectory, Func<HttpListenerRequest, bool> authorize,
            long maximumFileBytes = 64L * 1024 * 1024)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (!address.IsAbsoluteUri || (address.Scheme != "http" && address.Scheme != "https") ||
                !address.AbsolutePath.EndsWith("/", StringComparison.Ordinal) || address.Query.Length != 0 ||
                address.Fragment.Length != 0 || address.UserInfo.Length != 0)
                throw new ArgumentException("쿼리 없는 HTTP(S) 디렉터리 주소를 지정하세요.", nameof(address));
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("저장 디렉터리를 지정하세요.", nameof(rootDirectory));
            this.authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
            if (maximumFileBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
            prefix = address.AbsoluteUri;
            path = address.AbsolutePath;
            root = Path.GetFullPath(rootDirectory);
            this.maximumFileBytes = maximumFileBytes;
        }

        /// <inheritdoc />
        public bool IsRunning { get { lock (sync) return current != null && !current.Stopping && !current.Completion.IsCompleted; } }
        /// <inheritdoc />
        public Task Completion { get { lock (sync) return current?.Completion ?? Task.CompletedTask; } }
        /// <summary>최근 요청 오류입니다.</summary>
        public Exception LastRequestError => Volatile.Read(ref lastRequestError);

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException(nameof(FileTransferServer));
                if (current != null) throw new InvalidOperationException("서버가 이미 실행 중이거나 종료 중입니다.");
                Directory.CreateDirectory(root);
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("재분석 지점은 저장 디렉터리로 사용할 수 없습니다.");
                var run = new Run();
                run.Listener.Prefixes.Add(prefix);
                try { run.Listener.Start(); }
                catch { run.Listener.Close(); run.Cancellation.Dispose(); throw; }
                current = run;
                run.Completion = Task.Run(() => AcceptAsync(run));
                return Task.CompletedTask;
            }
        }

        private async Task AcceptAsync(Run run)
        {
            try
            {
                while (!run.Stopping)
                {
                    HttpListenerContext context = await run.Listener.GetContextAsync().ConfigureAwait(false);
                    if (run.Stopping) { Reject(context, HttpStatusCode.ServiceUnavailable); break; }
                    if (!run.Slots.Wait(0)) { Reject(context, HttpStatusCode.ServiceUnavailable); continue; }
                    Task task = Task.Run(async () =>
                    {
                        try { await HandleAsync(context, run.Cancellation.Token).ConfigureAwait(false); }
                        finally { run.Slots.Release(); }
                    });
                    lock (sync)
                    {
                        run.Requests.Add(task);
                        task.ContinueWith(completed =>
                        {
                            lock (sync) run.Requests.Remove(completed);
                        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }
                }
            }
            catch (Exception error) when (run.Stopping &&
                (error is HttpListenerException || error is ObjectDisposedException || error is InvalidOperationException)) { }
            catch (Exception error) { Volatile.Write(ref lastRequestError, error); }
            finally
            {
                bool signal;
                lock (sync) signal = BeginStop(run);
                SignalStop(run, signal);
                await run.StopSignaled.Task.ConfigureAwait(false);
                Task[] requests;
                lock (sync) requests = new List<Task>(run.Requests).ToArray();
                try { await Task.WhenAll(requests).ConfigureAwait(false); }
                finally
                {
                    run.Listener.Close();
                    run.Cancellation.Dispose();
                    run.Slots.Dispose();
                    lock (sync) if (current == run) current = null;
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext context, CancellationToken stop)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(60));
                using (deadline.Token.Register(() => { try { context.Response.Abort(); context.Request.InputStream.Close(); } catch (Exception) { } }))
                {
                    try
                    {
                        if (!authorize(context.Request)) { Reject(context, HttpStatusCode.Forbidden); return; }
                        string raw = context.Request.Url?.AbsolutePath;
                        if (context.Request.Url?.Query.Length > 0)
                        { Reject(context, HttpStatusCode.BadRequest); return; }
                        if (raw == null || !raw.StartsWith(path, StringComparison.Ordinal) || raw.Length <= path.Length)
                        { Reject(context, HttpStatusCode.BadRequest); return; }
                        string name = Uri.UnescapeDataString(raw.Substring(path.Length));
                        try { FileNamePolicy.Validate(name); }
                        catch (ArgumentException) { Reject(context, HttpStatusCode.BadRequest); return; }
                        string target = Path.Combine(root, name);
                        switch (context.Request.HttpMethod)
                        {
                            case "PUT": await UploadAsync(context, target, deadline.Token).ConfigureAwait(false); break;
                            case "GET": await DownloadAsync(context, target, deadline.Token).ConfigureAwait(false); break;
                            default: Reject(context, HttpStatusCode.MethodNotAllowed); break;
                        }
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                        error is HttpListenerException || error is OperationCanceledException || error is ObjectDisposedException)
                    {
                        if (!stop.IsCancellationRequested) Volatile.Write(ref lastRequestError, error);
                        try { Reject(context, HttpStatusCode.InternalServerError); } catch (Exception) { }
                    }
                    catch (Exception error)
                    {
                        Volatile.Write(ref lastRequestError, error);
                        try { Reject(context, HttpStatusCode.InternalServerError); } catch (Exception) { }
                    }
                    finally { try { context.Response.Close(); } catch (Exception) { } }
                }
            }
        }

        private async Task UploadAsync(HttpListenerContext context, string target, CancellationToken cancellationToken)
        {
            if (context.Request.ContentLength64 > maximumFileBytes)
            { Reject(context, HttpStatusCode.RequestEntityTooLarge); return; }
            string expected = context.Request.Headers[ContentHash.HeaderName];
            if (!ContentHash.IsValid(expected))
            { Reject(context, HttpStatusCode.BadRequest); return; }
            string temporary = Path.Combine(root, ".upload-" + Guid.NewGuid().ToString("N"));
            try
            {
                long total = 0;
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                using (var digest = SHA256.Create())
                {
                    byte[] buffer = new byte[81920];
                    int count;
                    while ((count = await context.Request.InputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                    {
                        total += count;
                        if (total > maximumFileBytes)
                        { Reject(context, HttpStatusCode.RequestEntityTooLarge); return; }
                        digest.TransformBlock(buffer, 0, count, null, 0);
                        await output.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                    }
                    digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (!ContentHash.Matches(expected, digest.Hash))
                    { Reject(context, HttpStatusCode.BadRequest); return; }
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (File.Exists(target))
                    {
                        if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                        { Reject(context, HttpStatusCode.Forbidden); return; }
                        File.Replace(temporary, target, null);
                    }
                    else File.Move(temporary, target);
                }
                finally { writeGate.Release(); }
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.ContentLength64 = 0;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private async Task DownloadAsync(HttpListenerContext context, string target, CancellationToken cancellationToken)
        {
            if (!File.Exists(target)) { Reject(context, HttpStatusCode.NotFound); return; }
            if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            { Reject(context, HttpStatusCode.Forbidden); return; }
            using (var input = new FileStream(target, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 81920, true))
            {
                if (input.Length > maximumFileBytes) { Reject(context, HttpStatusCode.RequestEntityTooLarge); return; }
                context.Response.Headers[ContentHash.HeaderName] =
                    ContentHash.ToHex(await ContentHash.ComputeAsync(input, cancellationToken).ConfigureAwait(false));
                input.Position = 0;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength64 = input.Length;
                await input.CopyToAsync(context.Response.OutputStream, 81920, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void Reject(HttpListenerContext context, HttpStatusCode status)
        {
            context.Response.StatusCode = (int)status;
            context.Response.ContentLength64 = 0;
            context.Response.Close();
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            bool signal;
            lock (sync)
            {
                run = current;
                signal = run != null && BeginStop(run);
            }
            if (run == null) return;
            SignalStop(run, signal);
            await run.Completion.ConfigureAwait(false);
        }
        private bool BeginStop(Run run)
        {
            if (run.Stopping) return false;
            run.Stopping = true;
            return true;
        }
        private void SignalStop(Run run, bool signal)
        {
            if (!signal) return;
            try
            {
                Cleanup(run.Listener.Stop);
                Cleanup(run.Cancellation.Cancel);
            }
            finally { run.StopSignaled.TrySetResult(true); }
        }
        private void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) { Volatile.Write(ref lastRequestError, error); }
        }
        /// <inheritdoc />
        public void Dispose()
        {
            Run run;
            bool signal;
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                run = current;
                signal = run != null && BeginStop(run);
            }
            if (run != null) SignalStop(run, signal);
        }
        private sealed class Run
        {
            internal readonly HttpListener Listener = new HttpListener();
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal readonly SemaphoreSlim Slots = new SemaphoreSlim(16, 16);
            internal readonly HashSet<Task> Requests = new HashSet<Task>();
            internal readonly TaskCompletionSource<bool> StopSignaled =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Completion = Task.CompletedTask;
            internal volatile bool Stopping;
        }
    }
}
