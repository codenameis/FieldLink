using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Freedom.Clients
{
    /// <summary>호출자가 지정한 16진수 명령을 교환하고 응답 앞부분을 선택적으로 제거합니다.</summary>
    public sealed class FreedomSerialClient
    {
        private readonly ISerialClient transport;
        private readonly IFrameBoundary boundary;
        private readonly Func<byte[], byte[], OperationResult> checkResponse;
        private readonly TimeSpan timeout;
        /// <summary>사용자 프로토콜의 응답 경계와 선택적인 상태 검사를 지정합니다.</summary>
        public FreedomSerialClient(ISerialClient transport, IFrameBoundary boundary,
            Func<byte[], byte[], OperationResult> checkResponse = null, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
            this.checkResponse = checkResponse; this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            if (this.timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        /// <summary>주소는 명령의 16진수이며 stx 매개변수로 응답에서 제거할 바이트 수를 지정합니다.</summary>
        public async Task<OperationResult<byte[]>> ReadAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            cancellationToken.ThrowIfCancellationRequested();
            int start = AddressParameters.ExtractParameter(ref address, "stx", 0);
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(address));
            byte[] request = address.ToHexBytes();
            byte[] response = await transport.ExchangeAsync(request, boundary, timeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (checkResponse != null)
            {
                var check = checkResponse(request, response);
                if (!check.IsSuccess)
                    return check.ConvertFailed<byte[]>();
            }
            return start < response.Length ? OperationResult.CreateSuccessResult(response.RemoveBegin(start))
                : new OperationResult<byte[]>("응답이 지정한 시작 위치보다 짧습니다.");
        }
        /// <summary>완성된 쓰기 명령을 교환합니다. 참고 구현처럼 명령 자체에 기록할 값이 포함되어 있어야 합니다.</summary>
        public async Task<OperationResult> WriteAsync(string command, CancellationToken cancellationToken = default(CancellationToken)) =>
            await ReadAsync(command, cancellationToken).ConfigureAwait(false);
    }
}
