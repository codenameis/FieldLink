using FieldLink.Communication;
using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.YASKAWA.Protocols;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.YASKAWA.Clients
{
    /// <summary>YRC 고속 이더넷 요청에 식별자를 배정하고 UDP 응답을 해석합니다. 전송 수명은 호출자가 관리합니다.</summary>
    public sealed class YrcHighEthernetUdpClient
    {
        private readonly IUdpClient transport;
        private readonly TimeSpan timeout;
        private int requestId = -1;
        /// <summary>호출자가 연 UDP 전송을 지정합니다. 자동 재전송하지 않습니다.</summary>
        public YrcHighEthernetUdpClient(IUdpClient transport, TimeSpan? timeout = null)
        { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout); }
        /// <summary>요청 빌더의 모든 명령을 실행합니다. 다른 요청 ID의 응답은 같은 제한 시간 안에서 건너뜁니다.</summary>
        public async Task<OperationResult<T>> ExecuteAsync<T>(YrcEthernetRequest<T> request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            byte id = unchecked((byte)Interlocked.Increment(ref requestId));
            byte[] packet = request.Build(id);
            // 이전 판정: 길이 >= 32 && bytes[11] == id 이면 Accept.
            // R-018: 다른 ID는 건너뛰고, 현재 요청의 응답은 전체 헤더 검증 후 수용한다.
            byte[] response = await transport.ExchangeAsync(packet, timeout,
                bytes => bytes.Length < 32 ? ResponseDisposition.Reject : bytes[11] != id ? ResponseDisposition.Ignore :
                    YrcHighEthernetProtocol.ValidateResponse(packet, bytes).IsSuccess ? ResponseDisposition.Accept : ResponseDisposition.Reject,
                cancellationToken).ConfigureAwait(false);
            return request.ParseResponse(response);
        }
        /// <summary>알람 등의 요청을 순서대로 실행하고 첫 실패에서 중단합니다. 다른 호출자의 명령을 배제하는 트랜잭션은 아닙니다.</summary>
        public async Task<OperationResult<T[]>> ExecuteSequenceAsync<T>(YrcEthernetRequest<T>[] requests, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (requests == null)
                throw new ArgumentNullException(nameof(requests));
            var values = new List<T>();
            foreach (var request in requests)
            {
                var result = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return OperationResult.CreateFailedResult<T[]>(result);
                if ((object)result.Content != null)
                    values.Add(result.Content);
            }
            return OperationResult.CreateSuccessResult(values.ToArray());
        }
    }
}
