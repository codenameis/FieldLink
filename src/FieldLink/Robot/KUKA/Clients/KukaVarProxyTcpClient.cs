using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.KUKA.Protocols;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.KUKA.Clients
{
    /// <summary>KUKAVARPROXY 변수 읽기·쓰기를 수행합니다. 전송 수명은 호출자가 관리합니다.</summary>
    public sealed class KukaVarProxyTcpClient
    {
        private readonly ITcpClient transport;
        private readonly TimeSpan timeout;
        private ushort messageId;
        private readonly IFrameBoundary boundary = new HeaderLengthFrame(4, bytes => 4 + bytes.Array[bytes.Offset + 2] * 256 + bytes.Array[bytes.Offset + 3]);
        /// <summary>명시적으로 연결한 전송과 전체 교환 제한 시간을 지정합니다.</summary>
        public KukaVarProxyTcpClient(ITcpClient transport, TimeSpan? timeout = null)
        { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout); }
        /// <summary>변수의 원시 바이트를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            ExecuteAsync(KukaVarProxyProtocol.BuildReadValueCommand(address ?? throw new ArgumentNullException(nameof(address))), cancellationToken);
        /// <summary>원본과 같은 기본 인코딩으로 변수를 읽습니다.</summary>
        public async Task<OperationResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ReadAsync(address, cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(Encoding.Default.GetString(read.Content)) : OperationResult.CreateFailedResult<string>(read);
        }
        /// <summary>문자열 변수 쓰기의 장치 응답을 확인합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, string value, CancellationToken cancellationToken = default(CancellationToken)) =>
            await ExecuteAsync(KukaVarProxyProtocol.BuildWriteValueCommand(address ?? throw new ArgumentNullException(nameof(address)), value ?? throw new ArgumentNullException(nameof(value))), cancellationToken).ConfigureAwait(false);
        /// <summary>기본 인코딩으로 바이트를 문자열로 변환하여 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken)) => WriteAsync(address, Encoding.Default.GetString(value ?? throw new ArgumentNullException(nameof(value))), cancellationToken);
        private Task<OperationResult<byte[]>> ExecuteAsync(byte[] core, CancellationToken token) => transport.ExecuteTransactionAsync(async tx =>
        {
            byte[] packet = KukaVarProxyProtocol.PackCommand(core, messageId++);
            byte[] response = await tx.ExchangeAsync(packet, boundary).ConfigureAwait(false);
            if (response.Length < 5 || response[0] != packet[0] || response[1] != packet[1] || response[4] != packet[4])
                return new OperationResult<byte[]>("KUKAVARPROXY 응답 ID 또는 읽기/쓰기 모드가 요청과 다릅니다.");
            return KukaVarProxyProtocol.ExtractActualData(response);
        }, timeout, token);
    }
}
