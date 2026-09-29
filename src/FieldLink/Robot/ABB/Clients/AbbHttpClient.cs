using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.ABB.Protocols;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.ABB.Clients
{
    /// <summary>ABB 요청의 HTTP 실행을 담당합니다. HttpClient와 인증 처리기의 생성·해제는 호출자가 담당합니다.</summary>
    public sealed class AbbHttpClient
    {
        private readonly HttpClient http;
        private readonly Uri baseAddress;
        private readonly TimeSpan timeout;
        /// <summary>공유할 HTTP 클라이언트와 컨트롤러 기본 주소를 지정합니다. 기본 주소나 공통 헤더를 변경하지 않습니다.</summary>
        public AbbHttpClient(HttpClient http, Uri baseAddress, TimeSpan? timeout = null)
        {
            this.http = http ?? throw new ArgumentNullException(nameof(http));
            if (baseAddress == null)
                throw new ArgumentNullException(nameof(baseAddress));
            if (!baseAddress.IsAbsoluteUri || (baseAddress.Scheme != "http" && baseAddress.Scheme != "https"))
                throw new ArgumentException("HTTP 또는 HTTPS 절대 주소가 필요합니다.", nameof(baseAddress));
            this.baseAddress = baseAddress; this.timeout = timeout ?? TimeSpan.FromSeconds(10); ClientOperation.ValidateTimeout(this.timeout);
        }
        /// <summary>ABB 요청 빌더의 조회를 실행합니다. HTTP 실패에는 상태 코드와 응답 본문을 보존합니다.</summary>
        public async Task<OperationResult<T>> ExecuteAsync<T>(AbbRequest<T> request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            var result = await SendAsync(HttpMethod.Get, request.Path, null, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? request.ParseResponse(result.Content) : OperationResult.CreateFailedResult<T>(result);
        }
        /// <summary>논리 주소 또는 url= 경로를 조회합니다.</summary>
        public Task<OperationResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            var request = AbbRequestBuilder.BuildByAddress(address);
            return request.IsSuccess ? ExecuteAsync(request.Content, cancellationToken) : Task.FromResult(OperationResult.CreateFailedResult<string>(request));
        }
        /// <summary>조회한 문자열을 UTF-8 바이트로 반환합니다.</summary>
        public async Task<OperationResult<byte[]>> ReadAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(Encoding.UTF8.GetBytes(result.Content)) : OperationResult.CreateFailedResult<byte[]>(result);
        }
        /// <summary>url= 경로에 미리 form 인코딩한 본문을 application/x-www-form-urlencoded로 POST합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, string value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (!address.StartsWith("url=", StringComparison.OrdinalIgnoreCase))
                return new OperationResult("쓰기는 url= 경로만 지원합니다.");
            return await SendAsync(HttpMethod.Post, address.Substring(4), value, cancellationToken).ConfigureAwait(false);
        }
        private async Task<OperationResult<string>> SendAsync(HttpMethod method, string path, string body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var uri = new Uri(baseAddress, path);
            if (uri.Scheme != baseAddress.Scheme || uri.Host != baseAddress.Host || uri.Port != baseAddress.Port)
                throw new ArgumentException("컨트롤러와 다른 서버의 경로를 사용할 수 없습니다.", nameof(path));
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var request = new HttpRequestMessage(method, uri))
            {
                deadline.CancelAfter(timeout);
                if (body != null)
                    request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
                using (var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, deadline.Token).ConfigureAwait(false))
                {
                    string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                    return response.IsSuccessStatusCode ? OperationResult.CreateSuccessResult(text) : new OperationResult<string>((int)response.StatusCode, text);
                }
            }
        }
    }
}
