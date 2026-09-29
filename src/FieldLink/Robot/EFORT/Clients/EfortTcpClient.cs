using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.EFORT.Protocols;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.EFORT.Clients
{
    /// <summary>EFORT 상태를 조회합니다. 주입한 전송의 열기·닫기·해제는 호출자가 담당합니다.</summary>
    public sealed class EfortTcpClient
    {
        private readonly ITcpClient transport;
        private readonly bool previousFormat;
        private readonly TimeSpan timeout;
        private readonly IFrameBoundary boundary;
        private ushort heartbeat;
        /// <summary>현재 형식 또는 이전 17바이트 헤더 형식을 지정합니다. 생성 시 연결하지 않습니다.</summary>
        public EfortTcpClient(ITcpClient transport, bool previousFormat = false, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); this.previousFormat = previousFormat;
            this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout);
            int header = previousFormat ? 17 : 18;
            boundary = new HeaderLengthFrame(header, bytes => header + Math.Max(0, BitConverter.ToInt16(bytes.Array, bytes.Offset + header - 2) - header));
        }
        /// <summary>전체 상태 프레임을 읽습니다. heartbeat는 실제 독점 교환 안에서 배정합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            transport.ExecuteTransactionAsync(async tx =>
            {
                byte[] command = previousFormat ? EfortProtocol.BuildPreviousReadCommand(heartbeat++) : EfortProtocol.BuildReadCommand(heartbeat++);
                return OperationResult.CreateSuccessResult(await tx.ExchangeAsync(command, boundary).ConfigureAwait(false));
            }, timeout, cancellationToken);
        /// <summary>상태 프레임을 데이터 모델로 해석합니다.</summary>
        public async Task<OperationResult<EfortControllerState>> ReadDataAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return previousFormat ? EfortControllerState.ParseFromPrevious(read.Content) : EfortControllerState.ParseFrom(read.Content);
        }
        /// <summary>상태 모델의 JSON 표현을 반환합니다.</summary>
        public async Task<OperationResult<string>> ReadStringAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ReadDataAsync(cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(JsonConvert.SerializeObject(result.Content, Formatting.Indented)) : OperationResult.CreateFailedResult<string>(result);
        }
    }
}
