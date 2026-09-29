using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.FileTransfer
{
    /// <summary>HTTP(S) 파일 전송 클라이언트입니다. 업로드·다운로드는 자동 재시도하지 않습니다.</summary>
    public sealed class FileTransferClient : IDisposable
    {
        private readonly Uri address;
        private readonly Action<HttpRequestMessage> configure;
        private readonly long maximumFileBytes;
        private readonly TimeSpan timeout;
        private readonly HttpClient client;

        /// <summary>요청별 인증 헤더는 configure에서 설정합니다.</summary>
        public FileTransferClient(Uri address, Action<HttpRequestMessage> configure = null,
            long maximumFileBytes = 64L * 1024 * 1024, TimeSpan? timeout = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (!address.IsAbsoluteUri || (address.Scheme != "http" && address.Scheme != "https") ||
                !address.AbsolutePath.EndsWith("/", StringComparison.Ordinal) || address.Query.Length != 0 ||
                address.Fragment.Length != 0 || address.UserInfo.Length != 0)
                throw new ArgumentException("쿼리 없는 HTTP(S) 디렉터리 주소를 지정하세요.", nameof(address));
            if (maximumFileBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
            this.address = address;
            this.configure = configure;
            this.maximumFileBytes = maximumFileBytes;
            this.timeout = timeout ?? TimeSpan.FromSeconds(70);
            if (this.timeout.TotalMilliseconds < 1 || this.timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = this.timeout };
        }

        /// <summary>로컬 파일을 원격 단일 이름으로 업로드합니다. 성공 시 기존 파일은 원자적으로 교체됩니다.</summary>
        public async Task UploadAsync(string localPath, string remoteName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (localPath == null) throw new ArgumentNullException(nameof(localPath));
            FileNamePolicy.Validate(remoteName);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                deadline.CancelAfter(timeout);
                if (input.Length > maximumFileBytes) throw new InvalidDataException("파일 크기 제한을 초과했습니다.");
                using (var request = CreateRequest(HttpMethod.Put, remoteName))
                {
                    request.Headers.TryAddWithoutValidation(ContentHash.HeaderName,
                        ContentHash.ToHex(await ContentHash.ComputeAsync(input, deadline.Token).ConfigureAwait(false)));
                    input.Position = 0;
                    request.Content = new StreamContent(input);
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                        deadline.Token).ConfigureAwait(false))
                        response.EnsureSuccessStatusCode();
                }
            }
        }

        /// <summary>원격 파일을 로컬 임시 파일로 받은 뒤 원자적으로 교체합니다.</summary>
        public async Task DownloadAsync(string remoteName, string localPath,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FileNamePolicy.Validate(remoteName);
            if (localPath == null) throw new ArgumentNullException(nameof(localPath));
            string target = Path.GetFullPath(localPath);
            string directory = Path.GetDirectoryName(target);
            string temporary = Path.Combine(directory, ".download-" + Guid.NewGuid().ToString("N"));
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var request = CreateRequest(HttpMethod.Get, remoteName))
            {
                deadline.CancelAfter(timeout);
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    deadline.Token).ConfigureAwait(false))
                using (deadline.Token.Register(() =>
                {
                    try { response.Dispose(); }
                    catch (Exception) { }
                }))
                {
                    try
                    {
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength > maximumFileBytes)
                            throw new InvalidDataException("파일 크기 제한을 초과했습니다.");
                        if (!response.Headers.TryGetValues(ContentHash.HeaderName, out var hashes))
                            throw new InvalidDataException("파일 SHA-256 응답이 없습니다.");
                        string[] values = hashes.ToArray();
                        if (values.Length != 1 || !ContentHash.IsValid(values[0]))
                            throw new InvalidDataException("파일 SHA-256 응답이 올바르지 않습니다.");
                        string expected = values[0];
                        try
                        {
                            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                                FileShare.None, 81920, true))
                            using (var digest = SHA256.Create())
                            {
                                long total = 0;
                                byte[] buffer = new byte[81920];
                                int count;
                                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) != 0)
                                {
                                    total += count;
                                    if (total > maximumFileBytes) throw new InvalidDataException("파일 크기 제한을 초과했습니다.");
                                    digest.TransformBlock(buffer, 0, count, null, 0);
                                    await output.WriteAsync(buffer, 0, count, deadline.Token).ConfigureAwait(false);
                                }
                                digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                                if (!ContentHash.Matches(expected, digest.Hash))
                                    throw new InvalidDataException("다운로드 파일의 SHA-256이 일치하지 않습니다.");
                                await output.FlushAsync(deadline.Token).ConfigureAwait(false);
                            }
                            deadline.Token.ThrowIfCancellationRequested();
                            if (File.Exists(target)) File.Replace(temporary, target, null);
                            else File.Move(temporary, target);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                    catch (Exception error) when (deadline.IsCancellationRequested &&
                        (error is IOException || error is ObjectDisposedException || error is HttpRequestException))
                    {
                        throw new OperationCanceledException("파일 다운로드가 취소되거나 제한 시간을 초과했습니다.",
                            error, deadline.Token);
                    }
                }
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string remoteName)
        {
            var request = new HttpRequestMessage(method, new Uri(address, Uri.EscapeDataString(remoteName)));
            try { configure?.Invoke(request); return request; }
            catch { request.Dispose(); throw; }
        }
        /// <inheritdoc />
        public void Dispose() => client.Dispose();
    }
}
