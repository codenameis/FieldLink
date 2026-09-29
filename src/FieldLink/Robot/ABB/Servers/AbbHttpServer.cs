using FieldLink.Communication;
using FieldLink.Robot.ABB.Protocols;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.ABB.Servers
{
    /// <summary>참조 ABB Web API의 고정 XHTML 응답을 제공하는 시뮬레이터입니다. 실제 제어 명령을 실행하지 않습니다.</summary>
    public sealed class AbbHttpServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly string prefix;
        private readonly TimeSpan responseTimeout;
        private Run current;
        private bool disposed;
        private Account account = new Account("Default User", "robotics");
        /// <summary>수신할 HTTP(S) 루트 URI를 지정합니다. 운영체제의 URL 예약·인증서 설정은 호출 환경에서 관리합니다.</summary>
        public AbbHttpServer(Uri baseAddress, TimeSpan? responseTimeout = null)
        {
            if (baseAddress == null)
                throw new ArgumentNullException(nameof(baseAddress));
            if (!baseAddress.IsAbsoluteUri || (baseAddress.Scheme != "http" && baseAddress.Scheme != "https") || baseAddress.AbsolutePath != "/" || baseAddress.Query.Length != 0 || baseAddress.Fragment.Length != 0 || baseAddress.UserInfo.Length != 0)
                throw new ArgumentException("HTTP(S) 루트 주소를 지정하세요.", nameof(baseAddress));
            prefix = baseAddress.AbsoluteUri;
            this.responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(10);
            if (this.responseTimeout.TotalMilliseconds < 1 || this.responseTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(responseTimeout));
        }
        /// <summary>Basic 인증 계정을 한 번에 변경합니다. 기본값은 참조 시뮬레이터와 같은 Default User / robotics입니다.</summary>
        public void SetLoginAccount(string name, string password)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            if (name.Contains(":"))
                throw new ArgumentException("사용자 이름에는 콜론을 사용할 수 없습니다.", nameof(name));
            Volatile.Write(ref account, new Account(name, password));
        }
        /// <summary>마지막 HTTP 응답 전송 오류입니다.</summary>
        public Exception LastRequestError { get; private set; }
        /// <inheritdoc />
        public bool IsRunning { get { lock (sync) return current != null && !current.Stopped && !current.Completion.IsCompleted; } }
        /// <inheritdoc />
        public Task Completion { get { lock (sync) return current?.Completion ?? Task.CompletedTask; } }
        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(AbbHttpServer));
                if (current != null)
                    throw new InvalidOperationException("서버가 이미 실행 중이거나 종료 중입니다.");
                var run = new Run();
                run.Listener.Prefixes.Add(prefix);
                try { run.Listener.Start(); }
                catch { run.Listener.Close(); throw; }
                current = run; run.Completion = Task.Run(() => ListenAsync(run));
                return Task.CompletedTask;
            }
        }
        private async Task ListenAsync(Run run)
        {
            try
            {
                while (!run.Stopped)
                {
                    var context = await run.Listener.GetContextAsync().ConfigureAwait(false);
                    try { await RespondAsync(context).ConfigureAwait(false); }
                    catch (Exception error) { LastRequestError = error; }
                    finally { try { context.Response.Close(); } catch (Exception) { } }
                }
            }
            catch (Exception) when (run.Stopped) { }
            finally { lock (sync) Stop(run); }
        }
        private async Task RespondAsync(HttpListenerContext context)
        {
            if (!Authenticate(context.Request.Headers["Authorization"]))
            {
                context.Response.StatusCode = 401;
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Secure Area\"";
                context.Response.ContentLength64 = 0;
                return;
            }
            string body = AbbSimulationResponseBuilder.Build(context.Request.RawUrl ?? "");
            if (body == null) { context.Response.StatusCode = 404; context.Response.ContentLength64 = 0; return; }
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "application/xhtml+xml; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            using (var deadline = new CancellationTokenSource(responseTimeout))
            using (deadline.Token.Register(() => { try { context.Response.Abort(); } catch (ObjectDisposedException) { } }))
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, deadline.Token).ConfigureAwait(false);
        }
        private bool Authenticate(string header)
        {
            if (header == null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return false;
            try
            {
                string value = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6).Trim()));
                int separator = value.IndexOf(':');
                if (separator < 0)
                    return false;
                Account expected = Volatile.Read(ref account);
                return value.Substring(0, separator) == expected.Name && value.Substring(separator + 1) == expected.Password;
            }
            catch (FormatException) { return false; }
        }
        /// <inheritdoc />
        public async Task StopAsync()
        {
            Run run;
            lock (sync)
            {
                run = current;
                if (run == null)
                    return;
                Stop(run);
            }

            try
            {
                await run.Completion.ConfigureAwait(false);
            }
            finally
            {
                lock (sync)
                    if (ReferenceEquals(current, run))
                        current = null;
            }
        }
        private static void Stop(Run run) { if (!run.Stopped) { run.Stopped = true; run.Listener.Close(); } }
        /// <summary>HTTP 수신을 즉시 중단합니다. 처리 중인 요청의 완료를 확인하려면 먼저 StopAsync를 기다리세요.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                if (current != null)
                    Stop(current);
            }
        }
        private sealed class Run
        {
            internal readonly HttpListener Listener = new HttpListener();
            internal volatile bool Stopped;
            internal Task Completion = Task.CompletedTask;
        }
        private sealed class Account
        {
            internal Account(string name, string password) { Name = name; Password = password; }
            internal readonly string Name, Password;
        }
    }
}
