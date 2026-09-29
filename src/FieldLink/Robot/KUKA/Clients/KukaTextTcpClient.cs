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
    /// <summary>KUKA 사용자 정의 텍스트 서버와 교환합니다. 서버 프로그램에 맞는 응답 경계를 반드시 지정합니다.</summary>
    /// <remarks>원본의 한 번의 소켓 수신을 프레임 완성으로 취급하지 않습니다. 전송의 수명은 호출자가 관리합니다.</remarks>
    public sealed class KukaTextTcpClient
    {
        private readonly ITcpClient transport;
        private readonly IFrameBoundary boundary;
        private readonly TimeSpan timeout;
        /// <summary>전송, 장치 프로그램의 응답 경계와 전체 제한 시간을 지정합니다.</summary>
        public KukaTextTcpClient(ITcpClient transport, IFrameBoundary responseBoundary, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); boundary = responseBoundary ?? throw new ArgumentNullException(nameof(responseBoundary));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout);
        }
        /// <summary>단일 또는 쉼표로 연결된 변수의 값을 읽습니다.</summary>
        public async Task<OperationResult<byte[]>> ReadAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            KukaTextProtocol.ParseReadResponse(await ExchangeAsync(KukaTextProtocol.BuildReadCommands(address ?? throw new ArgumentNullException(nameof(address))), cancellationToken).ConfigureAwait(false));
        /// <summary>응답 바이트를 UTF-8 문자열로 변환합니다.</summary>
        public async Task<OperationResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            OperationResult.CreateSuccessResult(Encoding.UTF8.GetString((await ReadAsync(address, cancellationToken).ConfigureAwait(false)).Content));
        /// <summary>변수 하나를 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string address, string value, CancellationToken cancellationToken = default(CancellationToken)) => WriteAsync(new[] { address }, new[] { value }, cancellationToken);
        /// <summary>원본의 기본 인코딩으로 바이트 값을 해석하여 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken)) => WriteAsync(address, Encoding.Default.GetString(value ?? throw new ArgumentNullException(nameof(value))), cancellationToken);
        /// <summary>여러 변수를 하나의 텍스트 명령으로 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string[] addresses, string[] values, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteCommandAsync(KukaTextProtocol.BuildWriteCommands(addresses, values), cancellationToken);
        /// <summary>프로그램을 시작합니다.</summary>
        public Task<OperationResult> StartProgramAsync(string program, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteCommandAsync(KukaTextProtocol.BuildStartProgram(program), cancellationToken);
        /// <summary>프로그램을 초기화합니다.</summary>
        public Task<OperationResult> ResetProgramAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteCommandAsync(KukaTextProtocol.BuildResetProgram(), cancellationToken);
        /// <summary>프로그램을 정지합니다.</summary>
        public Task<OperationResult> StopProgramAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteCommandAsync(KukaTextProtocol.BuildStopProgram(), cancellationToken);
        /// <summary>사용자 정의 명령의 오류 문자열을 검사합니다.</summary>
        public async Task<OperationResult> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default(CancellationToken)) => KukaTextProtocol.ParseCommandResponse(await ExchangeAsync(command, cancellationToken).ConfigureAwait(false));
        private Task<byte[]> ExchangeAsync(string command, CancellationToken token) => transport.ExchangeAsync(KukaTextProtocol.Encode(command), boundary, timeout, cancellationToken: token);
    }
}
